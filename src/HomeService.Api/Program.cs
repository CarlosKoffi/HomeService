using HomeService.Application.Abstractions;
using HomeService.Api;
using HomeService.Contracts.Companies;
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
    CancellationToken cancellationToken) =>
{
    var application = await db.CompanyApplications
        .Include(application => application.ActivationTokens)
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

    var rawToken = GenerateActivationToken();
    var tokenHash = HashActivationToken(rawToken);
    var expiresAt = DateTimeOffset.UtcNow.AddHours(GetActivationTokenDurationHours(configuration));
    var activationLink = BuildCompanyActivationLink(httpRequest, configuration, application.Id, rawToken);
    var previousStatus = application.Status;
    application.CreateActivationToken(tokenHash, expiresAt, "admin");
    AddCompanyApplicationStatusHistory(db, application.Id, previousStatus, application.Status, "Lien d'activation envoye.", "admin");
    QueueCompanyApplicantNotifications(
        db,
        application,
        "Votre portail entreprise est pret",
        $"Votre dossier est valide. Creez votre mot de passe avec ce lien valable jusqu'au {expiresAt:dd/MM/yyyy HH:mm} UTC.",
        includeWhatsApp: true,
        metadataJson: $$"""{"activationLink":"{{activationLink}}"}""");
    await db.SaveChangesAsync(cancellationToken);

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

static string HashActivationToken(string token)
{
    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
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

static string HashPassword(string password)
{
    var salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{salt}:{password}")));
    return $"sha256:{salt}:{hash}";
}

public partial class Program;
