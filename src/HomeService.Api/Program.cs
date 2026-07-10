using HomeService.Application.Abstractions;
using HomeService.Api;
using HomeService.Contracts.Branding;
using HomeService.Contracts.Companies;
using HomeService.Contracts.CompanyPortal;
using HomeService.Contracts.Localization;
using HomeService.Contracts.Notifications;
using HomeService.Contracts.Services;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "HomeService API",
        Version = "v1",
        Description = "API centrale pour la plateforme HomeService: services, entreprises, validation admin et futurs parcours client/prestataire."
    });
});
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<CompanyApplicationUploadService>();
builder.Services.AddSingleton<CompanyProviderUploadService>();

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

app.UseSiteAccessGate();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.DocumentTitle = "HomeService API";
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "HomeService API v1");
    options.RoutePrefix = "swagger";
});

if (string.Equals(app.Configuration["FORCE_HTTPS_REDIRECT"], "true", StringComparison.OrdinalIgnoreCase))
{
    app.UseHttpsRedirection();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "HomeService.Api" }))
    .WithName("HealthCheck");

app.MapGet("/api/services", async (IAppDbContext db, CancellationToken cancellationToken) =>
{
    var services = await db.Services
        .AsNoTracking()
        .OrderBy(service => service.Name)
        .Select(service => new ServiceSummaryResponse(
            service.Id,
            service.Name,
            service.Description,
            service.Status.ToString(),
            service.IsActive,
            service.NormalPriceAmount,
            service.PremiumPriceAmount,
            service.Currency))
        .ToListAsync(cancellationToken);

    return Results.Ok(services);
})
.WithName("ListServices");

app.MapGet("/api/translations", async (string? scope, string? language, string? country, IAppDbContext db, CancellationToken cancellationToken) =>
{
    var languageCode = string.IsNullOrWhiteSpace(language) ? "fr" : language.Trim().ToLowerInvariant();
    var countryCode = string.IsNullOrWhiteSpace(country) ? "CI" : country.Trim().ToUpperInvariant();

    var query = db.TranslationValues
        .AsNoTracking()
        .Where(value => value.Language!.Code == languageCode)
        .Where(value => value.Country == null || value.Country.IsoCode == countryCode)
        .Where(value => value.TranslationKey!.IsActive);

    if (!string.IsNullOrWhiteSpace(scope))
    {
        query = query.Where(value => value.TranslationKey!.Scope == scope.Trim());
    }

    var translations = await query
        .OrderBy(value => value.TranslationKey!.Scope)
        .ThenBy(value => value.TranslationKey!.Key)
        .Select(value => new TranslationValueResponse(
            value.TranslationKey!.Key,
            value.TranslationKey.Scope,
            value.Value))
        .ToListAsync(cancellationToken);

    return Results.Ok(translations);
})
.WithName("ListTranslations");

app.MapGet("/api/translations/dictionary", async (string? scope, string? language, string? country, IAppDbContext db, CancellationToken cancellationToken) =>
{
    var languageCode = string.IsNullOrWhiteSpace(language) ? "fr" : language.Trim().ToLowerInvariant();
    var countryCode = string.IsNullOrWhiteSpace(country) ? "CI" : country.Trim().ToUpperInvariant();

    var query = db.TranslationValues
        .AsNoTracking()
        .Where(value => value.Language!.Code == languageCode)
        .Where(value => value.Country == null || value.Country.IsoCode == countryCode)
        .Where(value => value.TranslationKey!.IsActive);

    if (!string.IsNullOrWhiteSpace(scope))
    {
        query = query.Where(value => value.TranslationKey!.Scope == scope.Trim());
    }

    var translations = await query
        .OrderBy(value => value.TranslationKey!.Scope)
        .ThenBy(value => value.TranslationKey!.Key)
        .Select(value => new
        {
            value.TranslationKey!.Key,
            value.Value
        })
        .ToDictionaryAsync(value => value.Key, value => value.Value, cancellationToken);

    return Results.Ok(translations);
})
.WithName("GetTranslationsDictionary");

app.MapGet("/api/country-branding", async (string? country, IAppDbContext db, CancellationToken cancellationToken) =>
{
    var countryCode = string.IsNullOrWhiteSpace(country) ? "CI" : country.Trim().ToUpperInvariant();
    var branding = await db.CountryBrandings
        .AsNoTracking()
        .Where(branding => branding.Country!.IsoCode == countryCode)
        .Select(branding => new CountryBrandingResponse(
            branding.Country!.IsoCode,
            branding.Country.Name,
            branding.BrandName,
            branding.PrimaryColor,
            branding.SecondaryColor,
            branding.AccentColor,
            branding.HeroTitle,
            branding.HeroSubtitle,
            branding.HeroImageUrl,
            branding.MotifStyle))
        .FirstOrDefaultAsync(cancellationToken);

    return branding is null ? Results.NotFound() : Results.Ok(branding);
})
.WithName("GetCountryBranding");

app.MapPost("/api/company-applications", async (
    HttpRequest httpRequest,
    IAppDbContext db,
    CompanyApplicationUploadService uploadService,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    try
    {
        if (!httpRequest.HasFormContentType)
        {
            return Results.BadRequest(new { message = "Le formulaire doit etre envoye au format multipart/form-data." });
        }

        logger.LogInformation("Company application submission received.");
        var form = await httpRequest.ReadFormAsync(cancellationToken);
        var request = new RegisterCompanyRequest(
            GetFormValue(form, "companyName"),
            GetOptionalFormValue(form, "registrationNumber"),
            GetFormValue(form, "city"),
            GetOptionalFormValue(form, "address"),
            GetFormValue(form, "contactName"),
            GetFormValue(form, "email"),
            GetFormValue(form, "phoneNumber"),
            GetServices(form),
            GetOptionalInt(form, "estimatedProviderCount"));

        var validationErrors = ValidateCompanyApplication(request);
        if (validationErrors.Count > 0)
        {
            return Results.BadRequest(new { message = "Le formulaire contient des erreurs.", errors = validationErrors });
        }

        var requiredDocumentFields = new[] { "fiscalExistenceDeclaration", "companyDocument", "ownerIdentityDocument" };
        if (requiredDocumentFields.Any(field => form.Files.GetFile(field) is null))
        {
            return Results.BadRequest(new { message = "Le DFE, le registre de commerce et l'identite du responsable sont obligatoires." });
        }

        var serviceNames = request.Services
            .Where(service => !string.IsNullOrWhiteSpace(service))
            .Select(service => service.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var application = new HomeService.Domain.Entities.CompanyApplication(
            request.CompanyName,
            request.RegistrationNumber,
            request.City,
            request.Address,
            request.ContactName,
            request.Email,
            request.PhoneNumber,
            serviceNames.Count > 0 ? string.Join(", ", serviceNames) : null,
            request.EstimatedProviderCount);

        db.CompanyApplications.Add(application);
        AddCompanyApplicationStatusHistory(
            db,
            application.Id,
            null,
            CompanyApplicationStatus.Submitted,
            "Demande soumise.",
            null);

        foreach (var serviceName in serviceNames)
        {
            db.CompanyApplicationServices.Add(new HomeService.Domain.Entities.CompanyApplicationService(application.Id, serviceName));
        }

        var documents = await uploadService.SaveAsync(application.Id, form.Files, cancellationToken);
        logger.LogInformation("Stored {DocumentCount} company application documents for {ApplicationId}.", documents.Count, application.Id);

        foreach (var document in documents)
        {
            db.CompanyApplicationDocuments.Add(new CompanyApplicationDocument(
                application.Id,
                document.DocumentType,
                document.OriginalFileName,
                document.StoragePath,
                document.ContentType));
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Company application {ApplicationId} saved.", application.Id);

        return Results.Created($"/api/admin/company-applications/{application.Id}", new { application.Id });
    }
    catch (InvalidOperationException exception)
    {
        logger.LogWarning(exception, "Company application submission rejected.");
        return Results.BadRequest(new { message = exception.Message });
    }
    catch (OperationCanceledException exception)
    {
        logger.LogWarning(exception, "Company application submission was cancelled while reading the form.");
        return Results.StatusCode(StatusCodes.Status499ClientClosedRequest);
    }
    catch (Microsoft.AspNetCore.Http.BadHttpRequestException exception)
    {
        logger.LogWarning(exception, "Company application submission was interrupted while reading uploaded files.");
        return Results.BadRequest(new { message = "L'envoi des pieces a ete interrompu. Verifiez la connexion puis relancez l'envoi." });
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Company application submission failed.");
        return Results.Problem(
            title: "Impossible d'enregistrer la demande entreprise",
            detail: exception.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
})
.WithName("RegisterCompanyApplication");

app.MapGet("/api/company-activation/{applicationId:guid}", async (
    Guid applicationId,
    string token,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var tokenHash = HashActivationToken(token);
    var activationToken = await db.CompanyActivationTokens
        .AsNoTracking()
        .Where(item => item.CompanyApplicationId == applicationId && item.TokenHash == tokenHash)
        .Select(item => new
        {
            Token = item,
            item.CompanyApplication!.CompanyName,
            item.CompanyApplication.Email
        })
        .FirstOrDefaultAsync(cancellationToken);

    if (activationToken is null || !activationToken.Token.IsActive)
    {
        return Results.BadRequest(new { message = "Ce lien d'activation est invalide ou expire." });
    }

    return Results.Ok(new CompanyActivationPreviewResponse(
        applicationId,
        activationToken.CompanyName,
        activationToken.Email,
        activationToken.Token.ExpiresAt));
})
.WithName("PreviewCompanyActivation");

app.MapPost("/api/company-portal/login", async (
    CompanyPortalLoginRequest request,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { message = "Email et mot de passe sont obligatoires." });
    }

    var email = request.Email.Trim().ToLowerInvariant();
    var user = await db.CompanyPortalUsers
        .Include(user => user.Company)
        .FirstOrDefaultAsync(user => user.Email == email && user.IsActive, cancellationToken);

    if (user is null || user.Company is null || !VerifyPassword(request.Password, user.PasswordHash))
    {
        return Results.Unauthorized();
    }

    if (user.Company.Status != CompanyStatus.Approved)
    {
        return Results.BadRequest(new { message = "Cette entreprise n'est pas encore active sur le portail." });
    }

    var token = GenerateSessionToken();
    var expiresAt = DateTimeOffset.UtcNow.AddDays(request.RememberMe ? 30 : 1);
    db.CompanyPortalSessions.Add(new CompanyPortalSession(user.Id, HashPortalToken(token), expiresAt));
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(new CompanyPortalLoginResponse(
        token,
        expiresAt,
        user.CompanyId,
        user.Company.Name,
        user.FullName,
        user.Email));
})
.WithName("LoginCompanyPortal");

app.MapGet("/api/company-portal/{companyId:guid}/employees", async (
    Guid companyId,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var exists = await db.Companies.AnyAsync(company => company.Id == companyId && company.Status == CompanyStatus.Approved, cancellationToken);
    if (!exists)
    {
        return Results.NotFound(new { message = "Entreprise introuvable ou inactive." });
    }

    var employees = await db.Providers
        .AsNoTracking()
        .Where(provider => provider.CompanyId == companyId && provider.Status != ProviderStatus.Inactive)
        .OrderBy(provider => provider.LastName)
        .ThenBy(provider => provider.FirstName)
        .Select(provider => new CompanyEmployeeResponse(
            provider.Id,
            provider.FirstName,
            provider.LastName,
            provider.PhoneNumber,
            provider.DateOfBirth,
            provider.Address,
            provider.Gender.ToString(),
            provider.EmploymentType.ToString(),
            provider.EmploymentType == ProviderEmploymentType.TemporaryWorker,
            provider.YearsOfExperience,
            provider.Status.ToString(),
            provider.IsAvailable,
            provider.MissionLatitude ?? provider.CurrentLatitude,
            provider.MissionLongitude ?? provider.CurrentLongitude,
            provider.MissionRadiusKm,
            provider.Documents
                .Where(document => document.DocumentType == ProviderDocumentType.Photo)
                .OrderByDescending(document => document.CreatedAt)
                .Select(document => $"/api/company-portal/provider-documents/{document.Id}/preview")
                .FirstOrDefault(),
            provider.Documents
                .Where(document => document.DocumentType == ProviderDocumentType.IdentityDocument)
                .OrderByDescending(document => document.CreatedAt)
                .Select(document => $"/api/company-portal/provider-documents/{document.Id}/preview")
                .FirstOrDefault(),
            provider.Documents
                .Where(document => document.DocumentType == ProviderDocumentType.Diploma)
                .OrderByDescending(document => document.CreatedAt)
                .Select(document => $"/api/company-portal/provider-documents/{document.Id}/preview")
                .FirstOrDefault(),
            provider.Documents.Any(document => document.DocumentType == ProviderDocumentType.Diploma),
            provider.Services
                .Where(providerService => providerService.IsActive)
                .OrderBy(providerService => providerService.Service!.Name)
                .Select(providerService => new CompanyEmployeeServiceResponse(
                    providerService.ServiceId,
                    providerService.Service!.Name,
                    providerService.ExperienceLevel.ToString(),
                    providerService.YearsOfExperience,
                    providerService.PriceTier.ToString(),
                    providerService.Service.NormalPriceAmount,
                    providerService.Service.PremiumPriceAmount,
                    providerService.Service.Currency,
                    providerService.IsActive))
                .ToList(),
            provider.Documents
                .OrderBy(document => document.DocumentType)
                .ThenByDescending(document => document.CreatedAt)
                .Select(document => new CompanyEmployeeDocumentResponse(
                    document.Id,
                    document.DocumentType.ToString(),
                    document.OriginalFileName,
                    document.ContentType,
                    $"/api/company-portal/provider-documents/{document.Id}/preview",
                    document.CreatedAt))
                .ToList(),
            provider.CreatedAt))
        .ToListAsync(cancellationToken);

    return Results.Ok(employees);
})
.WithName("ListCompanyPortalEmployees");

app.MapPost("/api/company-portal/{companyId:guid}/employees", async (
    Guid companyId,
    HttpRequest httpRequest,
    IAppDbContext db,
    CompanyProviderUploadService uploadService,
    CancellationToken cancellationToken) =>
{
    if (!httpRequest.HasFormContentType)
    {
        return Results.BadRequest(new { message = "Le formulaire employe doit etre envoye au format multipart/form-data." });
    }

    var company = await db.Companies.FirstOrDefaultAsync(company => company.Id == companyId && company.Status == CompanyStatus.Approved, cancellationToken);
    if (company is null)
    {
        return Results.NotFound(new { message = "Entreprise introuvable ou inactive." });
    }

    var form = await httpRequest.ReadFormAsync(cancellationToken);
    var errors = ValidateProviderForm(form);
    if (errors.Count > 0)
    {
        return Results.BadRequest(new { message = "Le formulaire employe contient des erreurs.", errors });
    }

    var provider = new ProviderProfile(
        companyId,
        GetFormValue(form, "firstName"),
        GetFormValue(form, "lastName"),
        GetFormValue(form, "phoneNumber"),
        DateOnly.Parse(GetFormValue(form, "dateOfBirth")),
        GetFormValue(form, "address"),
        ParseProviderGender(GetOptionalFormValue(form, "gender")),
        ParseProviderEmploymentType(GetOptionalFormValue(form, "employmentType")),
        GetOptionalInt(form, "yearsOfExperience") ?? 0,
        GetOptionalDecimal(form, "missionLatitude"),
        GetOptionalDecimal(form, "missionLongitude"),
        GetOptionalInt(form, "missionRadiusKm") ?? 5);

    var requestedServiceIds = GetGuidValues(form, "serviceIds");
    var services = await db.Services
        .Where(service => requestedServiceIds.Contains(service.Id) && service.IsActive)
        .Select(service => service.Id)
        .ToListAsync(cancellationToken);

    foreach (var serviceId in services)
    {
        provider.AddService(
            serviceId,
            ParseExperienceLevel(GetOptionalFormValue(form, "experienceLevel")));
    }

    db.Providers.Add(provider);

    var documents = await uploadService.SaveAsync(companyId, provider.Id, form.Files, cancellationToken);
    foreach (var document in documents)
    {
        provider.AttachDocument(new ProviderDocument(
            provider.Id,
            document.DocumentType,
            document.OriginalFileName,
            document.StoragePath,
            document.ContentType));
    }

    await db.SaveChangesAsync(cancellationToken);

    return Results.Created($"/api/company-portal/{companyId}/employees/{provider.Id}", new CreateCompanyEmployeeResult(provider.Id));
})
.WithName("CreateCompanyPortalEmployee");

app.MapPut("/api/company-portal/{companyId:guid}/employees/{employeeId:guid}", async (
    Guid companyId,
    Guid employeeId,
    UpdateCompanyEmployeeRequest request,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var provider = await db.Providers.FirstOrDefaultAsync(provider => provider.Id == employeeId && provider.CompanyId == companyId, cancellationToken);
    if (provider is null)
    {
        return Results.NotFound(new { message = "Employe introuvable." });
    }

    provider.UpdateCompanyProfile(
        request.FirstName,
        request.LastName,
        request.PhoneNumber,
        request.DateOfBirth,
        request.Address,
        ParseProviderGender(request.Gender),
        ParseProviderEmploymentType(request.EmploymentType),
        request.YearsOfExperience,
        request.MissionRadiusKm);

    await db.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
})
.WithName("UpdateCompanyPortalEmployee");

app.MapPut("/api/company-portal/{companyId:guid}/employees/{employeeId:guid}/services", async (
    Guid companyId,
    Guid employeeId,
    UpdateCompanyEmployeeServicesRequest request,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var provider = await db.Providers
        .Include(provider => provider.Services)
        .FirstOrDefaultAsync(provider => provider.Id == employeeId && provider.CompanyId == companyId, cancellationToken);
    if (provider is null)
    {
        return Results.NotFound(new { message = "Employe introuvable." });
    }

    var requestedIds = request.Services.Select(service => service.ServiceId).Distinct().ToList();
    var activeServiceIds = await db.Services
        .Where(service => requestedIds.Contains(service.Id) && service.IsActive)
        .Select(service => service.Id)
        .ToListAsync(cancellationToken);

    provider.SyncCompanyServices(request.Services
        .Where(service => activeServiceIds.Contains(service.ServiceId))
        .Select(service => (
            service.ServiceId,
            ParseExperienceLevel(service.ExperienceLevel),
            Math.Max(0, service.YearsOfExperience),
            ParseProviderServicePriceTier(service.PriceTier))));

    await db.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
})
.WithName("UpdateCompanyPortalEmployeeServices");

app.MapPost("/api/company-portal/{companyId:guid}/employees/{employeeId:guid}/documents", async (
    Guid companyId,
    Guid employeeId,
    HttpRequest httpRequest,
    IAppDbContext db,
    CompanyProviderUploadService uploadService,
    CancellationToken cancellationToken) =>
{
    if (!httpRequest.HasFormContentType)
    {
        return Results.BadRequest(new { message = "La piece doit etre envoyee au format multipart/form-data." });
    }

    var provider = await db.Providers
        .Include(provider => provider.Documents)
        .FirstOrDefaultAsync(provider => provider.Id == employeeId && provider.CompanyId == companyId, cancellationToken);
    if (provider is null)
    {
        return Results.NotFound(new { message = "Employe introuvable." });
    }

    var form = await httpRequest.ReadFormAsync(cancellationToken);
    if (!TryParseProviderDocumentType(GetOptionalFormValue(form, "documentType"), out var documentType))
    {
        return Results.BadRequest(new { message = "Type de piece invalide." });
    }

    var file = form.Files.GetFile("file");
    if (file is null)
    {
        return Results.BadRequest(new { message = "Aucun fichier recu." });
    }

    var oldDocuments = provider.Documents.Where(document => document.DocumentType == documentType).ToList();
    foreach (var oldDocument in oldDocuments)
    {
        TryDeleteProviderFile(uploadService, oldDocument.StoragePath);
        db.ProviderDocuments.Remove(oldDocument);
    }

    var stored = await uploadService.SaveOneAsync(companyId, provider.Id, documentType, file, cancellationToken);
    provider.AttachDocument(new ProviderDocument(provider.Id, stored.DocumentType, stored.OriginalFileName, stored.StoragePath, stored.ContentType));
    await db.SaveChangesAsync(cancellationToken);

    return Results.NoContent();
})
.WithName("UploadCompanyPortalEmployeeDocument");

app.MapDelete("/api/company-portal/{companyId:guid}/employees/{employeeId:guid}/documents/{documentId:guid}", async (
    Guid companyId,
    Guid employeeId,
    Guid documentId,
    IAppDbContext db,
    CompanyProviderUploadService uploadService,
    CancellationToken cancellationToken) =>
{
    var document = await db.ProviderDocuments
        .Include(document => document.Provider)
        .FirstOrDefaultAsync(document => document.Id == documentId && document.ProviderId == employeeId && document.Provider!.CompanyId == companyId, cancellationToken);
    if (document is null)
    {
        return Results.NotFound(new { message = "Piece introuvable." });
    }

    TryDeleteProviderFile(uploadService, document.StoragePath);
    db.ProviderDocuments.Remove(document);
    await db.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
})
.WithName("DeleteCompanyPortalEmployeeDocument");

app.MapPost("/api/company-portal/{companyId:guid}/employees/{employeeId:guid}/suspend", async (
    Guid companyId,
    Guid employeeId,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var provider = await db.Providers.FirstOrDefaultAsync(provider => provider.Id == employeeId && provider.CompanyId == companyId, cancellationToken);
    if (provider is null)
    {
        return Results.NotFound();
    }

    provider.SuspendByCompany();
    await db.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
})
.WithName("SuspendCompanyPortalEmployee");

app.MapDelete("/api/company-portal/{companyId:guid}/employees/{employeeId:guid}", async (
    Guid companyId,
    Guid employeeId,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var provider = await db.Providers.FirstOrDefaultAsync(provider => provider.Id == employeeId && provider.CompanyId == companyId, cancellationToken);
    if (provider is null)
    {
        return Results.NotFound();
    }

    provider.Deactivate();
    await db.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
})
.WithName("DeactivateCompanyPortalEmployee");

app.MapGet("/api/company-portal/provider-documents/{id:guid}/preview", async (
    Guid id,
    IAppDbContext db,
    CompanyProviderUploadService uploadService,
    CancellationToken cancellationToken) =>
{
    var document = await db.ProviderDocuments
        .AsNoTracking()
        .Where(document => document.Id == id)
        .Select(document => new
        {
            document.OriginalFileName,
            document.StoragePath,
            document.ContentType
        })
        .FirstOrDefaultAsync(cancellationToken);

    if (document is null)
    {
        return Results.NotFound();
    }

    var absolutePath = uploadService.GetAbsolutePath(document.StoragePath);
    if (!File.Exists(absolutePath))
    {
        return Results.NotFound(new { message = "Le fichier employe n'existe plus sur le serveur." });
    }

    return Results.File(absolutePath, document.ContentType, document.OriginalFileName, enableRangeProcessing: true);
})
.WithName("PreviewCompanyPortalProviderDocument");

app.MapGet("/api/company-portal/{companyId:guid}/missions", async (
    Guid companyId,
    string? view,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var exists = await db.Companies.AnyAsync(company => company.Id == companyId && company.Status == CompanyStatus.Approved, cancellationToken);
    if (!exists)
    {
        return Results.NotFound(new { message = "Entreprise introuvable ou inactive." });
    }

    var now = DateTimeOffset.UtcNow;
    var query = from mission in db.Missions.AsNoTracking()
                where mission.CompanyId == companyId
                join service in db.Services.AsNoTracking() on mission.ServiceId equals service.Id
                join customer in db.Customers.AsNoTracking() on mission.CustomerId equals customer.Id
                join provider in db.Providers.AsNoTracking() on mission.ProviderId equals provider.Id into providerJoin
                from provider in providerJoin.DefaultIfEmpty()
                select new { mission, service, customer, provider };

    query = view?.Trim().ToLowerInvariant() switch
    {
        "upcoming" => query.Where(row => row.mission.ScheduledFor >= now && row.mission.Status != MissionStatus.Completed && row.mission.Status != MissionStatus.Cancelled),
        "past" => query.Where(row => row.mission.Status == MissionStatus.Completed || row.mission.Status == MissionStatus.Cancelled),
        "live" => query.Where(row => row.mission.Status == MissionStatus.SearchingProvider || row.mission.Status == MissionStatus.Offered || row.mission.Status == MissionStatus.Accepted || row.mission.Status == MissionStatus.OnTheWay || row.mission.Status == MissionStatus.Started),
        _ => query
    };

    var missions = await query
        .OrderBy(row => row.mission.ScheduledFor ?? row.mission.CreatedAt)
        .Select(row => new CompanyPortalMissionResponse(
            row.mission.Id,
            row.service.Name,
            row.customer.FirstName + " " + row.customer.LastName,
            row.customer.PhoneNumber,
            row.mission.Mode.ToString(),
            row.mission.Status.ToString(),
            row.mission.PaymentMethod.ToString(),
            row.mission.PaymentStatus.ToString(),
            row.mission.ScheduledFor,
            row.mission.EstimatedDurationMinutes,
            row.mission.FinalTotalAmount ?? row.mission.EstimatedTotalAmount,
            row.mission.Currency,
            row.mission.ProviderId,
            row.provider == null ? null : row.provider.FirstName + " " + row.provider.LastName))
        .ToListAsync(cancellationToken);

    return Results.Ok(missions);
})
.WithName("ListCompanyPortalMissions");

app.MapGet("/api/company-portal/{companyId:guid}/payments", async (
    Guid companyId,
    string? period,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var exists = await db.Companies.AnyAsync(company => company.Id == companyId && company.Status == CompanyStatus.Approved, cancellationToken);
    if (!exists)
    {
        return Results.NotFound(new { message = "Entreprise introuvable ou inactive." });
    }

    var normalizedPeriod = period?.Trim().ToLowerInvariant() ?? "month";
    var start = GetPaymentPeriodStart(normalizedPeriod);
    var missions = await (from mission in db.Missions.AsNoTracking()
                          where mission.CompanyId == companyId
                              && mission.Status == MissionStatus.Completed
                              && (mission.ScheduledFor == null || mission.ScheduledFor >= start)
                          join service in db.Services.AsNoTracking() on mission.ServiceId equals service.Id
                          join customer in db.Customers.AsNoTracking() on mission.CustomerId equals customer.Id
                          join provider in db.Providers.AsNoTracking() on mission.ProviderId equals provider.Id into providerJoin
                          from provider in providerJoin.DefaultIfEmpty()
                          orderby mission.ScheduledFor descending
                          select new CompanyPortalMissionResponse(
                              mission.Id,
                              service.Name,
                              customer.FirstName + " " + customer.LastName,
                              customer.PhoneNumber,
                              mission.Mode.ToString(),
                              mission.Status.ToString(),
                              mission.PaymentMethod.ToString(),
                              mission.PaymentStatus.ToString(),
                              mission.ScheduledFor,
                              mission.EstimatedDurationMinutes,
                              mission.FinalTotalAmount ?? mission.EstimatedTotalAmount,
                              mission.Currency,
                              mission.ProviderId,
                              provider == null ? null : provider.FirstName + " " + provider.LastName))
        .ToListAsync(cancellationToken);

    return Results.Ok(new CompanyPortalPaymentSummaryResponse(
        normalizedPeriod,
        missions.Sum(mission => mission.FinalTotalAmount ?? 0),
        missions.Where(mission => mission.PaymentMethod == PaymentMethod.MobileMoney.ToString()).Sum(mission => mission.FinalTotalAmount ?? 0),
        missions.Where(mission => mission.PaymentMethod == PaymentMethod.Cash.ToString()).Sum(mission => mission.FinalTotalAmount ?? 0),
        missions.Where(mission => mission.PaymentMethod == PaymentMethod.Cash.ToString()).Sum(mission => mission.FinalTotalAmount ?? 0),
        missions.Count,
        "XOF",
        missions));
})
.WithName("GetCompanyPortalPayments");

var admin = app.MapGroup("/api/admin");

admin.MapGet("/company-applications", async (IAppDbContext db, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    try
    {
        var applications = await db.CompanyApplications
            .AsNoTracking()
            .OrderBy(application => application.Status == HomeService.Domain.Enums.CompanyApplicationStatus.Approved
                || application.Status == HomeService.Domain.Enums.CompanyApplicationStatus.ActivationSent
                || application.Status == HomeService.Domain.Enums.CompanyApplicationStatus.Activated)
            .ThenByDescending(application => application.SubmittedAt)
            .Select(application => new
            {
                application.Id,
                application.CompanyName,
                application.City,
                application.ContactName,
                application.Email,
                application.PhoneNumber,
                Status = application.Status.ToString(),
                application.SubmittedAt,
                application.LastReminderSentAt,
                application.ActivationEmailSentAt
            })
            .ToListAsync(cancellationToken);

        var applicationIds = applications.Select(application => application.Id).ToList();
        var documents = await db.CompanyApplicationDocuments
            .AsNoTracking()
            .Where(document => applicationIds.Contains(document.CompanyApplicationId))
            .OrderBy(document => document.DocumentType)
            .Select(document => new
            {
                document.CompanyApplicationId,
                document.Id,
                DocumentType = document.DocumentType.ToString(),
                ReviewStatus = document.ReviewStatus.ToString(),
                document.ReviewNote
            })
            .ToListAsync(cancellationToken);

        var documentsByApplication = documents
            .GroupBy(document => document.CompanyApplicationId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(document => new CompanyApplicationDocumentSummaryResponse(
                        document.Id,
                        document.DocumentType,
                        document.ReviewStatus,
                        document.ReviewNote))
                    .ToList());

        var response = applications
            .Select(application =>
            {
                documentsByApplication.TryGetValue(application.Id, out var applicationDocuments);
                applicationDocuments ??= [];

                return new CompanyApplicationSummaryResponse(
                    application.Id,
                    application.CompanyName,
                    application.City,
                    application.ContactName,
                    application.Email,
                    application.PhoneNumber,
                    application.Status,
                    application.SubmittedAt,
                    application.LastReminderSentAt,
                    application.ActivationEmailSentAt,
                    applicationDocuments.Count,
                    applicationDocuments.Count(document => document.ReviewStatus == HomeService.Domain.Enums.DocumentReviewStatus.Pending.ToString()),
                    applicationDocuments);
            })
            .ToList();

        return Results.Ok(response);
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Unable to list company applications.");
        return Results.Problem(
            title: "Unable to list company applications",
            detail: exception.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
})
.WithName("ListCompanyApplications");

admin.MapGet("/notifications", async (IAppDbContext db, CancellationToken cancellationToken) =>
{
    var notifications = await db.NotificationOutboxMessages
        .AsNoTracking()
        .OrderBy(notification => notification.Status)
        .ThenByDescending(notification => notification.ScheduledAt)
        .Take(100)
        .Select(notification => new NotificationOutboxMessageResponse(
            notification.Id,
            notification.Channel.ToString(),
            notification.Status.ToString(),
            notification.Recipient,
            notification.Subject,
            notification.Body,
            notification.RelatedEntityType,
            notification.RelatedEntityId,
            notification.ScheduledAt,
            notification.SentAt,
            notification.FailureReason))
        .ToListAsync(cancellationToken);

    return Results.Ok(notifications);
})
.WithName("ListNotificationOutboxMessages");

admin.MapGet("/country-brandings/{countryCode}", async (string countryCode, IAppDbContext db, CancellationToken cancellationToken) =>
{
    var normalizedCountryCode = countryCode.Trim().ToUpperInvariant();
    var branding = await db.CountryBrandings
        .AsNoTracking()
        .Where(branding => branding.Country!.IsoCode == normalizedCountryCode)
        .Select(branding => new CountryBrandingResponse(
            branding.Country!.IsoCode,
            branding.Country.Name,
            branding.BrandName,
            branding.PrimaryColor,
            branding.SecondaryColor,
            branding.AccentColor,
            branding.HeroTitle,
            branding.HeroSubtitle,
            branding.HeroImageUrl,
            branding.MotifStyle))
        .FirstOrDefaultAsync(cancellationToken);

    return branding is null ? Results.NotFound() : Results.Ok(branding);
})
.WithName("GetAdminCountryBranding");

admin.MapPut("/country-brandings/{countryCode}", async (
    string countryCode,
    UpdateCountryBrandingRequest request,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var validationError = ValidateCountryBrandingRequest(request);
    if (validationError is not null)
    {
        return Results.BadRequest(new { message = validationError });
    }

    var normalizedCountryCode = countryCode.Trim().ToUpperInvariant();
    var country = await db.Countries.FirstOrDefaultAsync(country => country.IsoCode == normalizedCountryCode, cancellationToken);
    if (country is null)
    {
        return Results.NotFound(new { message = "Pays introuvable." });
    }

    var branding = await db.CountryBrandings.FirstOrDefaultAsync(branding => branding.CountryId == country.Id, cancellationToken);
    if (branding is null)
    {
        branding = new CountryBranding(
            country.Id,
            request.BrandName,
            request.PrimaryColor,
            request.SecondaryColor,
            request.AccentColor,
            request.HeroTitle,
            request.HeroSubtitle,
            request.HeroImageUrl,
            request.MotifStyle);
        db.CountryBrandings.Add(branding);
    }
    else
    {
        branding.Update(
            request.BrandName,
            request.PrimaryColor,
            request.SecondaryColor,
            request.AccentColor,
            request.HeroTitle,
            request.HeroSubtitle,
            request.HeroImageUrl,
            request.MotifStyle);
    }

    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(new CountryBrandingResponse(
        country.IsoCode,
        country.Name,
        branding.BrandName,
        branding.PrimaryColor,
        branding.SecondaryColor,
        branding.AccentColor,
        branding.HeroTitle,
        branding.HeroSubtitle,
        branding.HeroImageUrl,
        branding.MotifStyle));
})
.WithName("UpdateAdminCountryBranding");

admin.MapGet("/company-applications/{id:guid}", async (Guid id, IAppDbContext db, CancellationToken cancellationToken) =>
{
    var application = await db.CompanyApplications
        .AsNoTracking()
        .Where(application => application.Id == id)
        .Select(application => new CompanyApplicationDetailResponse(
            application.Id,
            application.CompanyId,
            application.CompanyName,
            application.RegistrationNumber,
            application.City,
            application.Address,
            application.ContactName,
            application.Email,
            application.PhoneNumber,
            application.PlannedServices,
            application.EstimatedProviderCount,
            application.Status.ToString(),
            application.SubmittedAt,
            application.ReviewedAt,
            application.LastReminderSentAt,
            application.ActivationEmailSentAt,
            application.ActivatedAt,
            application.Company == null ? null : application.Company.AssignmentMode.ToString(),
            application.ActivationTokens
                .OrderByDescending(token => token.CreatedAt)
                .Select(token => token.ActivationLink)
                .FirstOrDefault(),
            application.ActivationTokens
                .OrderByDescending(token => token.CreatedAt)
                .Select(token => (DateTimeOffset?)token.ExpiresAt)
                .FirstOrDefault(),
            application.ReviewNote,
            application.Documents
                .OrderBy(document => document.DocumentType)
                .Select(document => new CompanyApplicationDocumentResponse(
                    document.Id,
                    document.DocumentType.ToString(),
                    document.OriginalFileName,
                    document.ContentType,
                    document.ReviewStatus.ToString(),
                    document.ReviewNote,
                    document.CreatedAt))
                .ToList(),
            application.StatusHistory
                .OrderBy(history => history.ChangedAt)
                .Select(history => new CompanyApplicationStatusHistoryResponse(
                    history.Id,
                    history.PreviousStatus == null ? null : history.PreviousStatus.ToString(),
                    history.NewStatus.ToString(),
                    history.Note,
                    history.ChangedBy,
                    history.ChangedAt))
                .ToList()))
        .FirstOrDefaultAsync(cancellationToken);

    return application is null ? Results.NotFound() : Results.Ok(application);
})
.WithName("GetCompanyApplication");

admin.MapPut("/companies/{id:guid}/assignment-mode", async (
    Guid id,
    UpdateCompanyAssignmentModeRequest request,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var company = await db.Companies.FirstOrDefaultAsync(company => company.Id == id, cancellationToken);
    if (company is null)
    {
        return Results.NotFound();
    }

    if (!TryParseCompanyAssignmentMode(request.AssignmentMode, out var assignmentMode))
    {
        return Results.BadRequest(new { message = "Mode d'affectation invalide." });
    }

    company.ChangeAssignmentMode(assignmentMode);
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(ToCompanyAssignmentModeResponse(company));
})
.WithName("UpdateCompanyAssignmentMode");

admin.MapPost("/company-applications/{id:guid}/approve", async (
    Guid id,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var application = await db.CompanyApplications
        .Include(application => application.Documents)
        .FirstOrDefaultAsync(application => application.Id == id, cancellationToken);
    if (application is null)
    {
        return Results.NotFound();
    }

    var requiredDocumentTypes = new[]
    {
        HomeService.Domain.Enums.CompanyDocumentType.FiscalExistenceDeclaration,
        HomeService.Domain.Enums.CompanyDocumentType.BusinessRegistration,
        HomeService.Domain.Enums.CompanyDocumentType.OwnerIdentity
    };
    var missingApprovedDocument = requiredDocumentTypes.Any(documentType =>
        !application.Documents.Any(document =>
            document.DocumentType == documentType
            && document.ReviewStatus == HomeService.Domain.Enums.DocumentReviewStatus.Approved));
    if (missingApprovedDocument)
    {
        return Results.BadRequest(new { message = "Les pieces obligatoires doivent etre validees avant validation du dossier." });
    }

    var previousStatus = application.Status;
    application.Approve("admin");
    AddCompanyApplicationStatusHistory(db, application.Id, previousStatus, application.Status, null, "admin");
    if (application.CompanyId is null)
    {
        var company = new Company(application.CompanyName, application.PhoneNumber, application.Email);
        company.Approve();
        db.Companies.Add(company);
        application.LinkApprovedCompany(company.Id, "admin");
    }

    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(ToCompanyApplicationActionResponse(application));
})
.WithName("ApproveCompanyApplication");

admin.MapPost("/company-applications/{id:guid}/reject", async (
    Guid id,
    CompanyApplicationReviewRequest request,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var note = GetRequiredReviewNote(request.Note, "Une note est obligatoire pour refuser une demande entreprise.");
    if (note.ErrorMessage is not null)
    {
        return Results.BadRequest(new { message = note.ErrorMessage });
    }

    var application = await db.CompanyApplications.FirstOrDefaultAsync(application => application.Id == id, cancellationToken);
    if (application is null)
    {
        return Results.NotFound();
    }

    var previousStatus = application.Status;
    application.Reject(note.Value!, "admin");
    AddCompanyApplicationStatusHistory(db, application.Id, previousStatus, application.Status, note.Value, "admin");
    QueueCompanyApplicantNotifications(
        db,
        application,
        "Votre dossier entreprise a ete refuse",
        $"Votre demande d'inscription entreprise a ete refusee. Motif: {note.Value}",
        includeWhatsApp: true);
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(ToCompanyApplicationActionResponse(application));
})
.WithName("RejectCompanyApplication");

admin.MapPost("/company-applications/{id:guid}/reopen", async (
    Guid id,
    CompanyApplicationReviewRequest request,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var note = GetRequiredReviewNote(request.Note, "Une note est obligatoire pour reouvrir une demande refusee.");
    if (note.ErrorMessage is not null)
    {
        return Results.BadRequest(new { message = note.ErrorMessage });
    }

    var application = await db.CompanyApplications.FirstOrDefaultAsync(application => application.Id == id, cancellationToken);
    if (application is null)
    {
        return Results.NotFound();
    }

    var previousStatus = application.Status;
    application.Reopen(note.Value!, "admin");
    AddCompanyApplicationStatusHistory(db, application.Id, previousStatus, application.Status, note.Value, "admin");
    QueueCompanyApplicantNotifications(
        db,
        application,
        "Votre dossier entreprise est reouvert",
        $"Votre dossier entreprise a ete reouvert pour une nouvelle verification. Note: {note.Value}",
        includeWhatsApp: true);
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(ToCompanyApplicationActionResponse(application));
})
.WithName("ReopenCompanyApplication");

admin.MapPost("/company-applications/{id:guid}/request-more-information", async (
    Guid id,
    CompanyApplicationReviewRequest request,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var note = GetRequiredReviewNote(request.Note, "Une note est obligatoire pour demander un complement.");
    if (note.ErrorMessage is not null)
    {
        return Results.BadRequest(new { message = note.ErrorMessage });
    }

    var application = await db.CompanyApplications.FirstOrDefaultAsync(application => application.Id == id, cancellationToken);
    if (application is null)
    {
        return Results.NotFound();
    }

    var previousStatus = application.Status;
    application.RequestMoreInformation(note.Value!, "admin");
    AddCompanyApplicationStatusHistory(db, application.Id, previousStatus, application.Status, note.Value, "admin");
    QueueCompanyApplicantNotifications(
        db,
        application,
        "Complement requis pour votre dossier entreprise",
        $"Nous avons besoin d'un complement pour finaliser votre dossier entreprise. Detail: {note.Value}",
        includeWhatsApp: true);
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(ToCompanyApplicationActionResponse(application));
})
.WithName("RequestCompanyApplicationMoreInformation");

admin.MapPost("/company-applications/{id:guid}/activation-link", async (
    Guid id,
    HttpRequest httpRequest,
    IAppDbContext db,
    IConfiguration configuration,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    var application = await db.CompanyApplications
        .FirstOrDefaultAsync(application => application.Id == id, cancellationToken);
    if (application is null)
    {
        return Results.NotFound();
    }

    if (application.Status is not HomeService.Domain.Enums.CompanyApplicationStatus.Approved
        and not HomeService.Domain.Enums.CompanyApplicationStatus.ActivationSent)
    {
        return Results.BadRequest(new { message = "Le lien d'activation ne peut etre genere qu'apres validation du dossier." });
    }

    if (application.ActivationEmailSentAt is not null)
    {
        application.MarkReminderSent();
    }

    string activationLink;
    DateTimeOffset expiresAt;
    try
    {
        var now = DateTimeOffset.UtcNow;
        await db.CompanyActivationTokens
            .Where(token => token.CompanyApplicationId == application.Id
                && token.UsedAt == null
                && token.RevokedAt == null
                && token.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.RevokedAt, now)
                .SetProperty(token => token.RevocationReason, "Remplace par un nouveau token d'activation."),
                cancellationToken);

        var rawToken = GenerateActivationToken();
        var tokenHash = HashActivationToken(rawToken);
        expiresAt = now.AddHours(GetActivationTokenDurationHours(configuration));
        activationLink = BuildCompanyActivationLink(httpRequest, configuration, application.Id, rawToken);
        var previousStatus = application.Status;
        application.CreateActivationToken(tokenHash, expiresAt, activationLink, "admin");
        AddCompanyApplicationStatusHistory(db, application.Id, previousStatus, application.Status, "Lien d'activation envoye.", "admin");
        await db.SaveChangesAsync(cancellationToken);
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Activation link generation failed for company application {ApplicationId}.", id);
        return Results.Problem(
            title: "Generation du lien d'activation impossible.",
            detail: exception.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }

    try
    {
        QueueCompanyApplicantNotifications(
            db,
            application,
            "Votre portail entreprise est pret",
            $"Votre dossier est valide. Creez votre mot de passe avec ce lien valable jusqu'au {expiresAt:dd/MM/yyyy HH:mm} UTC.",
            includeWhatsApp: true,
            metadataJson: $$"""{"activationLink":"{{activationLink}}"}""");
        await db.SaveChangesAsync(cancellationToken);
    }
    catch (Exception exception)
    {
        logger.LogWarning(exception, "Activation link was generated but notification outbox could not be queued for company application {ApplicationId}.", application.Id);
    }

    return Results.Ok(new CompanyApplicationActivationLinkResponse(
        application.Id,
        application.Status.ToString(),
        application.ActivationEmailSentAt,
        application.LastReminderSentAt,
        expiresAt,
        activationLink));
})
.WithName("GenerateCompanyApplicationActivationLink");

admin.MapPost("/company-application-documents/{id:guid}/approve", async (
    Guid id,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var document = await db.CompanyApplicationDocuments.FirstOrDefaultAsync(document => document.Id == id, cancellationToken);
    if (document is null)
    {
        return Results.NotFound();
    }

    document.Approve();
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(ToCompanyApplicationDocumentReviewResponse(document));
})
.WithName("ApproveCompanyApplicationDocument");

admin.MapPost("/company-application-documents/{id:guid}/reject", async (
    Guid id,
    CompanyApplicationDocumentReviewRequest request,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var comment = GetRequiredReviewNote(request.Comment, "Un commentaire est obligatoire pour refuser une piece.");
    if (comment.ErrorMessage is not null)
    {
        return Results.BadRequest(new { message = comment.ErrorMessage });
    }

    var document = await db.CompanyApplicationDocuments.FirstOrDefaultAsync(document => document.Id == id, cancellationToken);
    if (document is null)
    {
        return Results.NotFound();
    }

    document.Reject(comment.Value!);
    await QueueDocumentNotificationAsync(
        db,
        document.CompanyApplicationId,
        "Une piece de votre dossier a ete refusee",
        $"Une piece de votre dossier entreprise a ete refusee. Commentaire: {comment.Value}",
        cancellationToken);
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(ToCompanyApplicationDocumentReviewResponse(document));
})
.WithName("RejectCompanyApplicationDocument");

admin.MapPost("/company-application-documents/{id:guid}/request-replacement", async (
    Guid id,
    CompanyApplicationDocumentReviewRequest request,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var comment = GetRequiredReviewNote(request.Comment, "Un commentaire est obligatoire pour demander le remplacement d'une piece.");
    if (comment.ErrorMessage is not null)
    {
        return Results.BadRequest(new { message = comment.ErrorMessage });
    }

    var document = await db.CompanyApplicationDocuments.FirstOrDefaultAsync(document => document.Id == id, cancellationToken);
    if (document is null)
    {
        return Results.NotFound();
    }

    document.RequestReplacement(comment.Value!);
    await QueueDocumentNotificationAsync(
        db,
        document.CompanyApplicationId,
        "Complement de piece requis",
        $"Une piece de votre dossier entreprise doit etre remplacee ou completee. Commentaire: {comment.Value}",
        cancellationToken);
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(ToCompanyApplicationDocumentReviewResponse(document));
})
.WithName("RequestCompanyApplicationDocumentReplacement");

admin.MapPost("/company-application-documents/{id:guid}/reopen", async (
    Guid id,
    CompanyApplicationDocumentReviewRequest request,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var comment = GetRequiredReviewNote(request.Comment, "Un commentaire est obligatoire pour reouvrir une piece refusee.");
    if (comment.ErrorMessage is not null)
    {
        return Results.BadRequest(new { message = comment.ErrorMessage });
    }

    var document = await db.CompanyApplicationDocuments.FirstOrDefaultAsync(document => document.Id == id, cancellationToken);
    if (document is null)
    {
        return Results.NotFound();
    }

    document.Reopen(comment.Value!);
    await QueueDocumentNotificationAsync(
        db,
        document.CompanyApplicationId,
        "Une piece refusee est reouverte",
        $"Une piece de votre dossier a ete reouverte pour verification. Commentaire: {comment.Value}",
        cancellationToken);
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(ToCompanyApplicationDocumentReviewResponse(document));
})
.WithName("ReopenCompanyApplicationDocument");

admin.MapGet("/company-application-documents/{id:guid}/download", async (
    Guid id,
    IAppDbContext db,
    CompanyApplicationUploadService uploadService,
    CancellationToken cancellationToken) =>
{
    var document = await db.CompanyApplicationDocuments
        .AsNoTracking()
        .Where(document => document.Id == id)
        .Select(document => new
        {
            document.OriginalFileName,
            document.StoragePath,
            document.ContentType
        })
        .FirstOrDefaultAsync(cancellationToken);

    if (document is null)
    {
        return Results.NotFound();
    }

    string absolutePath;
    try
    {
        absolutePath = uploadService.GetAbsolutePath(document.StoragePath);
    }
    catch (InvalidOperationException)
    {
        return Results.BadRequest(new { message = "Chemin de document invalide." });
    }

    if (!File.Exists(absolutePath))
    {
        return Results.NotFound(new { message = "Le fichier n'existe plus sur le serveur." });
    }

    return Results.File(absolutePath, document.ContentType, document.OriginalFileName);
})
.WithName("DownloadCompanyApplicationDocument");

app.MapPost("/api/company-activation/password", async (
    CompanyActivationPasswordRequest request,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var validationError = ValidateActivationPasswordRequest(request);
    if (validationError is not null)
    {
        return Results.BadRequest(new { message = validationError });
    }

    var tokenHash = HashActivationToken(request.Token);
    var activationToken = await db.CompanyActivationTokens
        .Include(token => token.CompanyApplication)
        .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    if (activationToken is null || !activationToken.IsActive || activationToken.CompanyApplication is null)
    {
        return Results.BadRequest(new { message = "Le lien d'activation est invalide ou expire." });
    }

    var application = activationToken.CompanyApplication;
    var email = application.Email.ToLowerInvariant();
    var existingUser = await db.CompanyPortalUsers.AnyAsync(user => user.Email == email, cancellationToken);
    if (existingUser)
    {
        return Results.BadRequest(new { message = "Un compte portail existe deja pour cet email." });
    }

    Company company;
    if (application.CompanyId is { } companyId)
    {
        company = await db.Companies.FirstAsync(company => company.Id == companyId, cancellationToken);
    }
    else
    {
        company = new Company(application.CompanyName, application.PhoneNumber, application.Email);
        company.Approve();
        db.Companies.Add(company);
        application.LinkApprovedCompany(company.Id);
    }

    db.CompanyPortalUsers.Add(new CompanyPortalUser(company.Id, application.ContactName, email, HashPassword(request.Password), true));
    activationToken.MarkUsed();
    var previousStatus = application.Status;
    application.MarkActivated("activation");
    AddCompanyApplicationStatusHistory(db, application.Id, previousStatus, application.Status, "Compte entreprise active.", "activation");
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(new CompanyActivationPasswordResponse(true, "Mot de passe cree. Votre portail entreprise est pret."));
})
.WithName("CreateCompanyPasswordFromActivationToken");

app.Run();

static async Task<CompanyPortalSession?> GetCompanyPortalSessionAsync(
    HttpRequest request,
    IAppDbContext db,
    CancellationToken cancellationToken)
{
    var authorization = request.Headers.Authorization.ToString();
    const string bearerPrefix = "Bearer ";
    if (string.IsNullOrWhiteSpace(authorization) || !authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
    {
        return null;
    }

    var token = authorization[bearerPrefix.Length..].Trim();
    if (string.IsNullOrWhiteSpace(token))
    {
        return null;
    }

    var tokenHash = HashPortalToken(token);
    return await db.CompanyPortalSessions
        .Include(session => session.CompanyPortalUser)
        .ThenInclude(user => user!.Company)
        .FirstOrDefaultAsync(session => session.TokenHash == tokenHash && session.RevokedAt == null && session.ExpiresAt > DateTimeOffset.UtcNow, cancellationToken);
}

static async Task<IReadOnlyList<CompanyPortalEmployeeResponse>> GetCompanyEmployeesAsync(
    Guid companyId,
    IAppDbContext db,
    CancellationToken cancellationToken)
{
    var providers = await db.Providers
        .AsNoTracking()
        .Include(provider => provider.Documents)
        .Include(provider => provider.Services)
        .ThenInclude(providerService => providerService.Service)
        .Where(provider => provider.CompanyId == companyId)
        .OrderBy(provider => provider.LastName)
        .ThenBy(provider => provider.FirstName)
        .ToListAsync(cancellationToken);

    var serviceIds = providers
        .SelectMany(provider => provider.Services)
        .Select(service => service.ServiceId)
        .Distinct()
        .ToList();

    var serviceNames = await db.Services
        .AsNoTracking()
        .Where(service => serviceIds.Contains(service.Id))
        .ToDictionaryAsync(service => service.Id, service => service.Name, cancellationToken);

    return providers.Select(provider => MapCompanyPortalEmployee(provider, serviceNames)).ToList();
}

static CompanyPortalEmployeeResponse MapCompanyPortalEmployee(ProviderProfile provider, IReadOnlyDictionary<Guid, string> serviceNames)
{
    var photo = provider.Documents.FirstOrDefault(document => document.DocumentType == ProviderDocumentType.Photo);
    var identityDocument = provider.Documents.FirstOrDefault(document => document.DocumentType == ProviderDocumentType.IdentityDocument);
    var diploma = provider.Documents.FirstOrDefault(document => document.DocumentType == ProviderDocumentType.Diploma);

    return new CompanyPortalEmployeeResponse(
        Id: provider.Id,
        FirstName: provider.FirstName,
        LastName: provider.LastName,
        FullName: provider.FullName,
        PhoneNumber: provider.PhoneNumber,
        DateOfBirth: provider.DateOfBirth,
        Address: provider.Address,
        Gender: provider.Gender.ToString(),
        EmploymentType: provider.EmploymentType.ToString(),
        ReceivesDirectRequests: provider.EmploymentType == ProviderEmploymentType.TemporaryWorker,
        YearsOfExperience: provider.YearsOfExperience,
        Status: provider.Status.ToString(),
        IsAvailable: provider.IsAvailable,
        MissionLatitude: provider.MissionLatitude,
        MissionLongitude: provider.MissionLongitude,
        MissionRadiusKm: provider.MissionRadiusKm,
        PhotoUrl: photo is null ? null : $"/api/company-portal/provider-documents/{photo.Id}/download",
        IdentityDocumentName: identityDocument?.OriginalFileName,
        DiplomaDocumentName: diploma?.OriginalFileName,
        HasDiploma: diploma is not null,
        Services: provider.Services
            .OrderBy(service => serviceNames.TryGetValue(service.ServiceId, out var name) ? name : string.Empty)
            .Select(service => new CompanyPortalEmployeeServiceResponse(
                service.ServiceId,
                serviceNames.TryGetValue(service.ServiceId, out var name) ? name : "Service",
                service.ExperienceLevel.ToString(),
                service.YearsOfExperience,
                service.PriceTier.ToString(),
                service.Service?.NormalPriceAmount ?? 0,
                service.Service?.PremiumPriceAmount ?? 0,
                service.Service?.Currency ?? "XOF",
                service.IsActive))
            .ToList());
}

static async Task<IReadOnlyList<CompanyPortalMissionResponse>> GetCompanyMissionRowsAsync(
    Guid companyId,
    string? view,
    IAppDbContext db,
    CancellationToken cancellationToken)
{
    var now = DateTimeOffset.UtcNow;
    var query = from mission in db.Missions.AsNoTracking()
                where mission.CompanyId == companyId
                join service in db.Services.AsNoTracking() on mission.ServiceId equals service.Id
                join customer in db.Customers.AsNoTracking() on mission.CustomerId equals customer.Id
                join provider in db.Providers.AsNoTracking() on mission.ProviderId equals provider.Id into providerJoin
                from provider in providerJoin.DefaultIfEmpty()
                select new { mission, service, customer, provider };

    query = view?.Trim().ToLowerInvariant() switch
    {
        "upcoming" => query.Where(row => row.mission.ScheduledFor >= now && row.mission.Status != MissionStatus.Completed && row.mission.Status != MissionStatus.Cancelled),
        "past" => query.Where(row => row.mission.Status == MissionStatus.Completed || row.mission.Status == MissionStatus.Cancelled),
        "live" => query.Where(row => row.mission.Status == MissionStatus.SearchingProvider || row.mission.Status == MissionStatus.Offered || row.mission.Status == MissionStatus.Accepted || row.mission.Status == MissionStatus.OnTheWay || row.mission.Status == MissionStatus.Started),
        _ => query
    };

    return await query
        .OrderBy(row => row.mission.ScheduledFor ?? row.mission.CreatedAt)
        .Select(row => new CompanyPortalMissionResponse(
            row.mission.Id,
            row.service.Name,
            row.customer.FirstName + " " + row.customer.LastName,
            row.customer.PhoneNumber,
            row.mission.Mode.ToString(),
            row.mission.Status.ToString(),
            row.mission.PaymentMethod.ToString(),
            row.mission.PaymentStatus.ToString(),
            row.mission.ScheduledFor,
            row.mission.EstimatedDurationMinutes,
            row.mission.FinalTotalAmount ?? row.mission.EstimatedTotalAmount,
            row.mission.Currency,
            row.mission.ProviderId,
            row.provider == null ? null : row.provider.FirstName + " " + row.provider.LastName))
        .ToListAsync(cancellationToken);
}

static DateTimeOffset GetPaymentPeriodStart(string period)
{
    var now = DateTimeOffset.UtcNow;
    return period switch
    {
        "year" => new DateTimeOffset(now.Year, 1, 1, 0, 0, 0, TimeSpan.Zero),
        "week" => now.AddDays(-7),
        _ => new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero)
    };
}

static string GetFormValue(IFormCollection form, string key)
{
    return form.TryGetValue(key, out var value) ? value.ToString() : string.Empty;
}

static string? GetOptionalFormValue(IFormCollection form, string key)
{
    var value = GetFormValue(form, key);
    return string.IsNullOrWhiteSpace(value) ? null : value;
}

static int? GetOptionalInt(IFormCollection form, string key)
{
    return int.TryParse(GetFormValue(form, key), out var value) ? value : null;
}

static IReadOnlyList<string> GetServices(IFormCollection form)
{
    if (!form.TryGetValue("services", out var values))
    {
        return [];
    }

    return values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .SelectMany(value => value!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
}

static IReadOnlyList<Guid> GetGuidValues(IFormCollection form, string key)
{
    if (!form.TryGetValue(key, out var values))
    {
        return [];
    }

    return values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .SelectMany(value => value!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Select(value => Guid.TryParse(value, out var parsed) ? parsed : Guid.Empty)
        .Where(value => value != Guid.Empty)
        .Distinct()
        .ToList();
}

static IReadOnlyList<Guid> GetServiceIds(IFormCollection form)
{
    return GetGuidValues(form, "serviceIds");
}

static decimal? GetOptionalDecimal(IFormCollection form, string key)
{
    return decimal.TryParse(GetFormValue(form, key), out var value) ? value : null;
}

static ExperienceLevel ParseExperienceLevel(string? value)
{
    return Enum.TryParse<ExperienceLevel>(value, true, out var level)
        ? level
        : ExperienceLevel.Confirmed;
}

static ProviderEmploymentType ParseProviderEmploymentType(string? value)
{
    return Enum.TryParse<ProviderEmploymentType>(value, true, out var employmentType)
        ? employmentType
        : ProviderEmploymentType.CompanyEmployee;
}

static ProviderGender ParseProviderGender(string? value)
{
    return Enum.TryParse<ProviderGender>(value, true, out var gender)
        ? gender
        : ProviderGender.Unspecified;
}

static ProviderServicePriceTier ParseProviderServicePriceTier(string? value)
{
    return Enum.TryParse<ProviderServicePriceTier>(value, true, out var tier)
        ? tier
        : ProviderServicePriceTier.Normal;
}

static bool TryParseProviderDocumentType(string? value, out ProviderDocumentType documentType)
{
    return Enum.TryParse(value?.Trim(), true, out documentType);
}

static void TryDeleteProviderFile(CompanyProviderUploadService uploadService, string storagePath)
{
    try
    {
        var absolutePath = uploadService.GetAbsolutePath(storagePath);
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }
    }
    catch (IOException)
    {
    }
    catch (UnauthorizedAccessException)
    {
    }
}

static bool TryParseCompanyAssignmentMode(string? value, out CompanyAssignmentMode assignmentMode)
{
    return Enum.TryParse(value?.Trim(), true, out assignmentMode);
}

static CompanyAssignmentModeResponse ToCompanyAssignmentModeResponse(Company company)
{
    var additionalCommissionRate = company.AssignmentMode == CompanyAssignmentMode.PlatformManaged ? 0.05m : 0m;
    var message = company.AssignmentMode == CompanyAssignmentMode.PlatformManaged
        ? "La plateforme affecte les missions. Une commission additionnelle pourra etre appliquee."
        : "L'entreprise affecte elle-meme les missions depuis son portail.";

    return new CompanyAssignmentModeResponse(
        company.Id,
        company.AssignmentMode.ToString(),
        additionalCommissionRate,
        message);
}

static IReadOnlyList<string> ValidateProviderForm(IFormCollection form)
{
    var errors = new List<string>();

    if (GetFormValue(form, "firstName").Trim().Length < 2)
    {
        errors.Add("Le prenom de l'employe est obligatoire.");
    }

    if (GetFormValue(form, "lastName").Trim().Length < 2)
    {
        errors.Add("Le nom de l'employe est obligatoire.");
    }

    var phoneDigits = Regex.Replace(GetFormValue(form, "phoneNumber"), @"\D", string.Empty);
    if (phoneDigits.Length is < 8 or > 15)
    {
        errors.Add("Le telephone de l'employe doit contenir entre 8 et 15 chiffres.");
    }

    if (!DateOnly.TryParse(GetFormValue(form, "dateOfBirth"), out var birthDate))
    {
        errors.Add("La date de naissance est obligatoire.");
    }
    else if (birthDate > DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-16)))
    {
        errors.Add("L'employe doit avoir au moins 16 ans.");
    }

    if (GetFormValue(form, "address").Trim().Length < 4)
    {
        errors.Add("L'adresse de l'employe est obligatoire.");
    }

    var yearsOfExperience = GetOptionalInt(form, "yearsOfExperience") ?? -1;
    if (yearsOfExperience is < 0 or > 60)
    {
        errors.Add("Le nombre d'annees d'experience doit etre compris entre 0 et 60.");
    }

    var radius = GetOptionalInt(form, "missionRadiusKm") ?? 0;
    if (radius is < 1 or > 100)
    {
        errors.Add("Le perimetre de mission doit etre compris entre 1 et 100 km.");
    }

    if (GetGuidValues(form, "serviceIds").Count == 0)
    {
        errors.Add("Au moins un service maitrise est obligatoire.");
    }

    if (form.Files.GetFile("photo") is null)
    {
        errors.Add("La photo de l'employe est obligatoire.");
    }

    if (form.Files.GetFile("identityDocument") is null)
    {
        errors.Add("La piece d'identite de l'employe est obligatoire.");
    }

    return errors;
}

static string? ValidateEmployeeForm(IFormCollection form)
{
    var errors = ValidateProviderForm(form);
    return errors.Count == 0 ? null : string.Join(" ", errors);
}

static void AddCompanyApplicationStatusHistory(
    IAppDbContext db,
    Guid companyApplicationId,
    CompanyApplicationStatus? previousStatus,
    CompanyApplicationStatus newStatus,
    string? note,
    string? changedBy)
{
    if (previousStatus.HasValue && previousStatus.Value == newStatus)
    {
        return;
    }

    db.CompanyApplicationStatusHistories.Add(new CompanyApplicationStatusHistory(
        companyApplicationId,
        previousStatus,
        newStatus,
        note,
        changedBy));
}

static void QueueCompanyApplicantNotifications(
    IAppDbContext db,
    HomeService.Domain.Entities.CompanyApplication application,
    string subject,
    string body,
    bool includeWhatsApp,
    string? metadataJson = null)
{
    if (!string.IsNullOrWhiteSpace(application.Email))
    {
        db.NotificationOutboxMessages.Add(new NotificationOutboxMessage(
            NotificationChannel.Email,
            application.Email,
            subject,
            body,
            nameof(HomeService.Domain.Entities.CompanyApplication),
            application.Id,
            metadataJson));
    }

    if (includeWhatsApp && !string.IsNullOrWhiteSpace(application.PhoneNumber))
    {
        db.NotificationOutboxMessages.Add(new NotificationOutboxMessage(
            NotificationChannel.WhatsApp,
            application.PhoneNumber,
            subject,
            body,
            nameof(HomeService.Domain.Entities.CompanyApplication),
            application.Id,
            metadataJson));
    }
}

static async Task QueueDocumentNotificationAsync(
    IAppDbContext db,
    Guid companyApplicationId,
    string subject,
    string body,
    CancellationToken cancellationToken)
{
    var application = await db.CompanyApplications
        .FirstOrDefaultAsync(application => application.Id == companyApplicationId, cancellationToken);

    if (application is null)
    {
        return;
    }

    QueueCompanyApplicantNotifications(db, application, subject, body, includeWhatsApp: true);
}

static IReadOnlyList<string> ValidateCompanyApplication(RegisterCompanyRequest request)
{
    var errors = new List<string>();

    if (request.CompanyName.Trim().Length < 3)
    {
        errors.Add("Le nom legal de l'entreprise doit contenir au moins 3 caracteres.");
    }

    if (!string.IsNullOrWhiteSpace(request.RegistrationNumber) && request.RegistrationNumber.Trim().Length < 4)
    {
        errors.Add("Le numero legal semble trop court.");
    }

    if (request.City.Trim().Length < 2)
    {
        errors.Add("La ville est obligatoire.");
    }

    if (request.ContactName.Trim().Length < 3)
    {
        errors.Add("Le nom du responsable est obligatoire.");
    }

    if (!Regex.IsMatch(request.Email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase))
    {
        errors.Add("L'email professionnel n'est pas valide.");
    }

    var phoneDigits = Regex.Replace(request.PhoneNumber, @"\D", string.Empty);
    if (phoneDigits.Length is < 8 or > 15)
    {
        errors.Add("Le telephone doit contenir entre 8 et 15 chiffres.");
    }

    if (request.EstimatedProviderCount is not null and (< 1 or > 10000))
    {
        errors.Add("Le nombre de prestataires doit etre compris entre 1 et 10000.");
    }

    if (request.Services.Count == 0)
    {
        errors.Add("Au moins un service est requis.");
    }

    return errors;
}

static CompanyApplicationActionResponse ToCompanyApplicationActionResponse(HomeService.Domain.Entities.CompanyApplication application)
{
    return new CompanyApplicationActionResponse(
        application.Id,
        application.Status.ToString(),
        application.ReviewedAt,
        application.ReviewNote);
}

static CompanyApplicationDocumentReviewResponse ToCompanyApplicationDocumentReviewResponse(CompanyApplicationDocument document)
{
    return new CompanyApplicationDocumentReviewResponse(
        document.Id,
        document.CompanyApplicationId,
        document.ReviewStatus.ToString(),
        document.ReviewNote);
}

static (string? Value, string? ErrorMessage) GetRequiredReviewNote(string? value, string requiredMessage)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return (null, requiredMessage);
    }

    var trimmed = value.Trim();
    if (trimmed.Length > 1000)
    {
        return (null, "La note ne peut pas depasser 1000 caracteres.");
    }

    return (trimmed, null);
}

static string BuildCompanyActivationLink(HttpRequest request, IConfiguration configuration, Guid applicationId, string token)
{
    var configuredBaseUrl =
        configuration["CompanyPortal:BaseUrl"]
        ?? configuration["COMPANY_PORTAL_BASE_URL"]
        ?? configuration["CompanyPortalBaseUrl"];

    var baseUrl = string.IsNullOrWhiteSpace(configuredBaseUrl)
        ? $"{request.Scheme}://{request.Host}"
        : configuredBaseUrl.Trim();

    return $"{baseUrl.TrimEnd('/')}/activate-company/{applicationId:D}?token={Uri.EscapeDataString(token)}";
}

static string GenerateActivationToken()
{
    return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
}

static string GenerateSessionToken()
{
    return GenerateActivationToken();
}

static string HashActivationToken(string token)
{
    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

static string HashPortalToken(string token)
{
    return HashActivationToken(token);
}

static int GetActivationTokenDurationHours(IConfiguration configuration)
{
    var configuredValue = configuration["CompanyPortal:ActivationTokenHours"] ?? configuration["COMPANY_ACTIVATION_TOKEN_HOURS"];
    return int.TryParse(configuredValue, out var hours) && hours is >= 1 and <= 720
        ? hours
        : 72;
}

static string? ValidateActivationPasswordRequest(CompanyActivationPasswordRequest request)
{
    if (string.IsNullOrWhiteSpace(request.Token))
    {
        return "Le token d'activation est obligatoire.";
    }

    if (request.Password.Length < 10)
    {
        return "Le mot de passe doit contenir au moins 10 caracteres.";
    }

    if (!request.Password.Any(char.IsUpper) || !request.Password.Any(char.IsLower) || !request.Password.Any(char.IsDigit))
    {
        return "Le mot de passe doit contenir une majuscule, une minuscule et un chiffre.";
    }

    if (request.Password != request.ConfirmPassword)
    {
        return "Les deux mots de passe ne correspondent pas.";
    }

    return null;
}

static string? ValidateCountryBrandingRequest(UpdateCountryBrandingRequest request)
{
    if (string.IsNullOrWhiteSpace(request.BrandName) || request.BrandName.Trim().Length > 120)
    {
        return "Le nom de marque est obligatoire et limite a 120 caracteres.";
    }

    if (!IsHexColor(request.PrimaryColor) || !IsHexColor(request.SecondaryColor) || !IsHexColor(request.AccentColor))
    {
        return "Les couleurs doivent etre au format hexadecimal, par exemple #f97316.";
    }

    if (string.IsNullOrWhiteSpace(request.HeroTitle) || request.HeroTitle.Trim().Length > 220)
    {
        return "Le titre hero est obligatoire et limite a 220 caracteres.";
    }

    if (string.IsNullOrWhiteSpace(request.HeroSubtitle) || request.HeroSubtitle.Trim().Length > 600)
    {
        return "Le sous-titre hero est obligatoire et limite a 600 caracteres.";
    }

    if (!string.IsNullOrWhiteSpace(request.HeroImageUrl)
        && (!Uri.TryCreate(request.HeroImageUrl, UriKind.Absolute, out var heroUri)
            || heroUri.Scheme is not ("http" or "https")))
    {
        return "L'image hero doit etre une URL http ou https valide.";
    }

    if (string.IsNullOrWhiteSpace(request.MotifStyle) || request.MotifStyle.Trim().Length > 80)
    {
        return "Le motif visuel est obligatoire et limite a 80 caracteres.";
    }

    return null;
}

static bool IsHexColor(string value)
{
    return Regex.IsMatch(value.Trim(), "^#[0-9a-fA-F]{6}$");
}

static string HashPassword(string password)
{
    var salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{salt}:{password}")));
    return $"sha256:{salt}:{hash}";
}

static bool VerifyPassword(string password, string passwordHash)
{
    var parts = passwordHash.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (parts.Length != 3 || !string.Equals(parts[0], "sha256", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{parts[1]}:{password}")));
    return CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(expectedHash),
        Encoding.UTF8.GetBytes(parts[2]));
}

public partial class Program;
