using HomeService.Application.Abstractions;
using HomeService.Api;
using HomeService.Contracts.Companies;
using HomeService.Contracts.Localization;
using HomeService.Contracts.Services;
using HomeService.Domain.Entities;
using HomeService.Infrastructure;
using Microsoft.EntityFrameworkCore;
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
            service.IsActive))
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
        var documentCounts = await db.CompanyApplicationDocuments
            .AsNoTracking()
            .Where(document => applicationIds.Contains(document.CompanyApplicationId))
            .GroupBy(document => document.CompanyApplicationId)
            .Select(group => new
            {
                CompanyApplicationId = group.Key,
                DocumentCount = group.Count(),
                PendingDocumentCount = group.Count(document => document.ReviewStatus == HomeService.Domain.Enums.DocumentReviewStatus.Pending)
            })
            .ToDictionaryAsync(item => item.CompanyApplicationId, cancellationToken);

        var response = applications
            .Select(application =>
            {
                documentCounts.TryGetValue(application.Id, out var counts);
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
                    counts?.DocumentCount ?? 0,
                    counts?.PendingDocumentCount ?? 0);
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

admin.MapGet("/company-applications/{id:guid}", async (Guid id, IAppDbContext db, CancellationToken cancellationToken) =>
{
    var application = await db.CompanyApplications
        .AsNoTracking()
        .Where(application => application.Id == id)
        .Select(application => new CompanyApplicationDetailResponse(
            application.Id,
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
                .ToList()))
        .FirstOrDefaultAsync(cancellationToken);

    return application is null ? Results.NotFound() : Results.Ok(application);
})
.WithName("GetCompanyApplication");

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

app.Run();

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

public partial class Program;
