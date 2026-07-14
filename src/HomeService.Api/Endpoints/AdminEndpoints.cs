using HomeService.Application.Abstractions;
using HomeService.Application.Admin;
using HomeService.Application.Auditing;
using HomeService.Application.Branding;
using HomeService.Application.Companies;
using HomeService.Application.Notifications;
using HomeService.Api.Auditing;
using HomeService.Contracts.Branding;
using HomeService.Contracts.Companies;
using HomeService.Contracts.Monitoring;
using HomeService.Contracts.Notifications;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin");
        
        admin.MapGet("/audit-logs", async (
            string? actorType,
            Guid? actorId,
            string? action,
            string? entityType,
            Guid? entityId,
            string? search,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int? skip,
            int? take,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var pageSize = Math.Clamp(take ?? 50, 1, 200);
            var offset = Math.Max(skip ?? 0, 0);
            var query = db.AuditLogEntries.AsNoTracking();
        
            if (Enum.TryParse<AuditActorType>(actorType, true, out var parsedActorType))
            {
                query = query.Where(entry => entry.ActorType == parsedActorType);
            }
        
            if (actorId.HasValue)
            {
                query = query.Where(entry => entry.ActorId == actorId.Value);
            }
        
            if (!string.IsNullOrWhiteSpace(action))
            {
                var normalizedAction = action.Trim();
                query = query.Where(entry => entry.Action == normalizedAction);
            }
        
            if (!string.IsNullOrWhiteSpace(entityType))
            {
                var normalizedEntityType = entityType.Trim();
                query = query.Where(entry => entry.EntityType == normalizedEntityType);
            }
        
            if (entityId.HasValue)
            {
                query = query.Where(entry => entry.EntityId == entityId.Value);
            }
        
            if (from.HasValue)
            {
                query = query.Where(entry => entry.OccurredAt >= from.Value);
            }
        
            if (to.HasValue)
            {
                query = query.Where(entry => entry.OccurredAt <= to.Value);
            }
        
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                query = query.Where(entry =>
                    entry.Action.ToLower().Contains(term)
                    || entry.EntityType.ToLower().Contains(term)
                    || (entry.ActorDisplayName != null && entry.ActorDisplayName.ToLower().Contains(term))
                    || (entry.Summary != null && entry.Summary.ToLower().Contains(term)));
            }
        
            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(entry => entry.OccurredAt)
                .Skip(offset)
                .Take(pageSize)
                .Select(entry => new AuditLogEntryResponse(
                    entry.Id,
                    entry.ActorType.ToString(),
                    entry.ActorId,
                    entry.ActorDisplayName,
                    entry.Action,
                    entry.EntityType,
                    entry.EntityId,
                    entry.Summary,
                    entry.BeforeJson,
                    entry.AfterJson,
                    entry.IpAddress,
                    entry.UserAgent,
                    entry.CorrelationId,
                    entry.OccurredAt))
                .ToListAsync(cancellationToken);
        
            return Results.Ok(new AuditLogListResponse(total, offset, pageSize, items));
        })
        .WithName("ListAdminAuditLogs");
        
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
            HttpRequest httpRequest,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var validationError = CountryBrandingValidator.Validate(request);
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
            object? before = null;
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
                before = new
                {
                    branding.BrandName,
                    branding.PrimaryColor,
                    branding.SecondaryColor,
                    branding.AccentColor,
                    branding.HeroTitle,
                    branding.HeroSubtitle,
                    branding.HeroImageUrl,
                    branding.MotifStyle
                };
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
        
            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminCountryBrandingUpdated",
                nameof(CountryBranding),
                branding.Id,
                $"Branding pays {country.IsoCode} mis a jour.",
                before,
                after: request);
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
            HttpRequest httpRequest,
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
        
            var previousMode = company.AssignmentMode;
            company.ChangeAssignmentMode(assignmentMode);
            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminCompanyAssignmentModeUpdated",
                nameof(Company),
                company.Id,
                "Mode d'affectation entreprise modifie.",
                before: new { AssignmentMode = previousMode },
                after: new { company.AssignmentMode });
            await db.SaveChangesAsync(cancellationToken);
        
            return Results.Ok(CompanyAssignmentModePresenter.ToResponse(company));
        })
        .WithName("UpdateCompanyAssignmentMode");
        
        admin.MapPost("/company-applications/{id:guid}/approve", async (
            Guid id,
            HttpRequest httpRequest,
            AdminCompanyApplicationReviewService reviewService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await reviewService.ApproveAsync(id, cancellationToken);
            var error = ToAdminCompanyApplicationReviewError(result);
            if (error is not null)
            {
                return error;
            }
        
            var application = result.Application!;
            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminCompanyApplicationApproved",
                nameof(HomeService.Domain.Entities.CompanyApplication),
                application.Id,
                "Demande entreprise validee.",
                before: new { Status = result.PreviousStatus },
                after: new { application.Status, application.CompanyId });
            await db.SaveChangesAsync(cancellationToken);
        
            return Results.Ok(ToCompanyApplicationActionResponse(application));
        })
        .WithName("ApproveCompanyApplication");
        
        admin.MapPost("/company-applications/{id:guid}/reject", async (
            Guid id,
            CompanyApplicationReviewRequest request,
            HttpRequest httpRequest,
            AdminCompanyApplicationReviewService reviewService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await reviewService.RejectAsync(id, request.Note, cancellationToken);
            var error = ToAdminCompanyApplicationReviewError(result);
            if (error is not null)
            {
                return error;
            }
        
            var application = result.Application!;
            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminCompanyApplicationRejected",
                nameof(HomeService.Domain.Entities.CompanyApplication),
                application.Id,
                "Demande entreprise refusee.",
                before: new { Status = result.PreviousStatus },
                after: new { application.Status, application.ReviewNote });
            await db.SaveChangesAsync(cancellationToken);
        
            return Results.Ok(ToCompanyApplicationActionResponse(application));
        })
        .WithName("RejectCompanyApplication");
        
        admin.MapPost("/company-applications/{id:guid}/reopen", async (
            Guid id,
            CompanyApplicationReviewRequest request,
            HttpRequest httpRequest,
            AdminCompanyApplicationReviewService reviewService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await reviewService.ReopenAsync(id, request.Note, cancellationToken);
            var error = ToAdminCompanyApplicationReviewError(result);
            if (error is not null)
            {
                return error;
            }
        
            var application = result.Application!;
            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminCompanyApplicationReopened",
                nameof(HomeService.Domain.Entities.CompanyApplication),
                application.Id,
                "Demande entreprise reouverte.",
                before: new { Status = result.PreviousStatus },
                after: new { application.Status, application.ReviewNote });
            await db.SaveChangesAsync(cancellationToken);
        
            return Results.Ok(ToCompanyApplicationActionResponse(application));
        })
        .WithName("ReopenCompanyApplication");
        
        admin.MapPost("/company-applications/{id:guid}/request-more-information", async (
            Guid id,
            CompanyApplicationReviewRequest request,
            HttpRequest httpRequest,
            AdminCompanyApplicationReviewService reviewService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await reviewService.RequestMoreInformationAsync(id, request.Note, cancellationToken);
            var error = ToAdminCompanyApplicationReviewError(result);
            if (error is not null)
            {
                return error;
            }
        
            var application = result.Application!;
            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminCompanyApplicationMoreInformationRequested",
                nameof(HomeService.Domain.Entities.CompanyApplication),
                application.Id,
                "Complement demande sur un dossier entreprise.",
                before: new { Status = result.PreviousStatus },
                after: new { application.Status, application.ReviewNote });
            await db.SaveChangesAsync(cancellationToken);
        
            return Results.Ok(ToCompanyApplicationActionResponse(application));
        })
        .WithName("RequestCompanyApplicationMoreInformation");
        
        admin.MapPost("/company-applications/{id:guid}/activation-link", async (
            Guid id,
            HttpRequest httpRequest,
            CompanyActivationLinkGenerationService activationLinkService,
            IAppDbContext db,
            IConfiguration configuration,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await activationLinkService.GenerateAsync(
                    id,
                    GetCompanyPortalBaseUrl(httpRequest, configuration),
                    GetActivationTokenDurationHours(configuration),
                    "admin",
                    cancellationToken);
        
                if (result.Status == CompanyActivationLinkGenerationStatus.NotFound)
                {
                    return Results.NotFound(new { message = result.Message });
                }
        
                if (result.Status == CompanyActivationLinkGenerationStatus.InvalidStatus)
                {
                    return Results.BadRequest(new { message = result.Message });
                }
        
                var response = result.Response!;
                AddAuditLog(
                    db,
                    httpRequest,
                    AuditActor.Admin(),
                    "AdminCompanyActivationLinkGenerated",
                    nameof(HomeService.Domain.Entities.CompanyApplication),
                    response.Id,
                    "Lien d'activation entreprise genere.",
                    before: new { Status = result.PreviousStatus },
                    after: new { response.Status, response.ExpiresAt, response.ActivationLink });
                await db.SaveChangesAsync(cancellationToken);
                return Results.Ok(response);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Activation link generation failed for company application {ApplicationId}.", id);
                return Results.Problem(
                    title: "Generation du lien d'activation impossible.",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("GenerateCompanyApplicationActivationLink");
        
        admin.MapPost("/company-application-documents/{id:guid}/approve", async (
            Guid id,
            HttpRequest httpRequest,
            AdminCompanyApplicationDocumentReviewService documentReviewService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await documentReviewService.ApproveAsync(id, cancellationToken);
            var error = ToAdminCompanyApplicationDocumentReviewError(result);
            if (error is not null)
            {
                return error;
            }
        
            var document = result.Document!;
            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminCompanyApplicationDocumentApproved",
                nameof(CompanyApplicationDocument),
                document.Id,
                "Piece entreprise validee.",
                before: new { Status = result.PreviousStatus },
                after: new { document.ReviewStatus });
            await db.SaveChangesAsync(cancellationToken);
        
            return Results.Ok(ToCompanyApplicationDocumentReviewResponse(document));
        })
        .WithName("ApproveCompanyApplicationDocument");
        
        admin.MapPost("/company-application-documents/{id:guid}/reject", async (
            Guid id,
            CompanyApplicationDocumentReviewRequest request,
            HttpRequest httpRequest,
            AdminCompanyApplicationDocumentReviewService documentReviewService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await documentReviewService.RejectAsync(id, request.Comment, cancellationToken);
            var error = ToAdminCompanyApplicationDocumentReviewError(result);
            if (error is not null)
            {
                return error;
            }
        
            var document = result.Document!;
            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminCompanyApplicationDocumentRejected",
                nameof(CompanyApplicationDocument),
                document.Id,
                "Piece entreprise refusee.",
                before: new { Status = result.PreviousStatus },
                after: new { document.ReviewStatus, document.ReviewNote });
            await db.SaveChangesAsync(cancellationToken);
        
            return Results.Ok(ToCompanyApplicationDocumentReviewResponse(document));
        })
        .WithName("RejectCompanyApplicationDocument");
        
        admin.MapPost("/company-application-documents/{id:guid}/request-replacement", async (
            Guid id,
            CompanyApplicationDocumentReviewRequest request,
            HttpRequest httpRequest,
            AdminCompanyApplicationDocumentReviewService documentReviewService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await documentReviewService.RequestReplacementAsync(id, request.Comment, cancellationToken);
            var error = ToAdminCompanyApplicationDocumentReviewError(result);
            if (error is not null)
            {
                return error;
            }
        
            var document = result.Document!;
            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminCompanyApplicationDocumentReplacementRequested",
                nameof(CompanyApplicationDocument),
                document.Id,
                "Remplacement de piece entreprise demande.",
                before: new { Status = result.PreviousStatus },
                after: new { document.ReviewStatus, document.ReviewNote });
            await db.SaveChangesAsync(cancellationToken);
        
            return Results.Ok(ToCompanyApplicationDocumentReviewResponse(document));
        })
        .WithName("RequestCompanyApplicationDocumentReplacement");
        
        admin.MapPost("/company-application-documents/{id:guid}/reopen", async (
            Guid id,
            CompanyApplicationDocumentReviewRequest request,
            HttpRequest httpRequest,
            AdminCompanyApplicationDocumentReviewService documentReviewService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await documentReviewService.ReopenAsync(id, request.Comment, cancellationToken);
            var error = ToAdminCompanyApplicationDocumentReviewError(result);
            if (error is not null)
            {
                return error;
            }
        
            var document = result.Document!;
            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminCompanyApplicationDocumentReopened",
                nameof(CompanyApplicationDocument),
                document.Id,
                "Piece entreprise reouverte.",
                before: new { Status = result.PreviousStatus },
                after: new { document.ReviewStatus, document.ReviewNote });
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
        return app;
    }
    static bool TryParseCompanyAssignmentMode(string? value, out CompanyAssignmentMode assignmentMode)
    {
        return Enum.TryParse(value?.Trim(), true, out assignmentMode);
    }
    
    static CompanyApplicationActionResponse ToCompanyApplicationActionResponse(HomeService.Domain.Entities.CompanyApplication application)
    {
        return new CompanyApplicationActionResponse(
            application.Id,
            application.Status.ToString(),
            application.ReviewedAt,
            application.ReviewNote);
    }
    
    static IResult? ToAdminCompanyApplicationReviewError(AdminCompanyApplicationReviewResult result)
    {
        return result.Status switch
        {
            AdminCompanyApplicationReviewStatus.Ok => null,
            AdminCompanyApplicationReviewStatus.NotFound => Results.NotFound(),
            AdminCompanyApplicationReviewStatus.ValidationFailed => Results.BadRequest(new { message = result.Message }),
            AdminCompanyApplicationReviewStatus.MissingRequiredApprovedDocuments => Results.BadRequest(new { message = result.Message }),
            AdminCompanyApplicationReviewStatus.InvalidTransition => Results.BadRequest(new { message = result.Message }),
            _ => Results.BadRequest(new { message = result.Message ?? "Action impossible." })
        };
    }
    
    static IResult? ToAdminCompanyApplicationDocumentReviewError(AdminCompanyApplicationDocumentReviewResult result)
    {
        return result.Status switch
        {
            AdminCompanyApplicationDocumentReviewStatus.Ok => null,
            AdminCompanyApplicationDocumentReviewStatus.NotFound => Results.NotFound(),
            AdminCompanyApplicationDocumentReviewStatus.ValidationFailed => Results.BadRequest(new { message = result.Message }),
            AdminCompanyApplicationDocumentReviewStatus.InvalidTransition => Results.BadRequest(new { message = result.Message }),
            _ => Results.BadRequest(new { message = result.Message ?? "Action impossible." })
        };
    }
    
    static CompanyApplicationDocumentReviewResponse ToCompanyApplicationDocumentReviewResponse(CompanyApplicationDocument document)
    {
        return new CompanyApplicationDocumentReviewResponse(
            document.Id,
            document.CompanyApplicationId,
            document.ReviewStatus.ToString(),
            document.ReviewNote);
    }
    
    static string GetCompanyPortalBaseUrl(HttpRequest request, IConfiguration configuration)
    {
        var configuredBaseUrl =
            configuration["CompanyPortal:BaseUrl"]
            ?? configuration["COMPANY_PORTAL_BASE_URL"]
            ?? configuration["CompanyPortalBaseUrl"];
    
        return string.IsNullOrWhiteSpace(configuredBaseUrl)
            ? $"{request.Scheme}://{request.Host}"
            : configuredBaseUrl.Trim();
    }
    
    static int GetActivationTokenDurationHours(IConfiguration configuration)
    {
        var configuredValue = configuration["CompanyPortal:ActivationTokenHours"] ?? configuration["COMPANY_ACTIVATION_TOKEN_HOURS"];
        return CompanyActivationTokenLifetimeResolver.ResolveHours(configuredValue);
    }
    
    static void AddAuditLog(
        IAppDbContext db,
        HttpRequest request,
        AuditActor actor,
        string action,
        string entityType,
        Guid? entityId,
        string? summary,
        object? before = null,
        object? after = null)
    {
        db.AuditLogEntries.Add(AuditLogFactory.Create(
            actor,
            action,
            entityType,
            entityId,
            summary,
            GetAuditRequestContext(request),
            before,
            after));
    }
    
    static AuditRequestContext GetAuditRequestContext(HttpRequest request)
    {
        return HttpAuditContextFactory.Create(request);
    }
}