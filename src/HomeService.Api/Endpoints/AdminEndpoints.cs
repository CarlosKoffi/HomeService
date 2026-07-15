using HomeService.Application.Abstractions;
using HomeService.Application.Admin;
using HomeService.Application.Auditing;
using HomeService.Application.Branding;
using HomeService.Application.Companies;
using HomeService.Application.Notifications;
using HomeService.Api.Auditing;
using HomeService.Contracts.Branding;
using HomeService.Contracts.Cms;
using HomeService.Contracts.Companies;
using HomeService.Contracts.Monitoring;
using HomeService.Contracts.Notifications;
using HomeService.Contracts.Services;
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
            AdminQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            var result = await queryService.ListAuditLogsAsync(new AdminAuditLogQuery(
                actorType,
                actorId,
                action,
                entityType,
                entityId,
                search,
                from,
                to,
                skip,
                take), cancellationToken);

            return Results.Ok(result);
        })
        .WithName("ListAdminAuditLogs");

        admin.MapGet("/cms/sites", async (
            AdminCmsQueryService cmsQueryService,
            CancellationToken cancellationToken) =>
        {
            var sites = await cmsQueryService.ListSitesAsync(cancellationToken);
            return Results.Ok(sites);
        })
        .WithName("ListAdminCmsSites")
        .Produces<IReadOnlyList<CmsSiteSummaryResponse>>();

        admin.MapGet("/cms/sites/{id:guid}", async (
            Guid id,
            AdminCmsQueryService cmsQueryService,
            CancellationToken cancellationToken) =>
        {
            var site = await cmsQueryService.GetSiteAsync(id, cancellationToken);
            return site is null ? Results.NotFound() : Results.Ok(site);
        })
        .WithName("GetAdminCmsSite")
        .Produces<CmsSiteDetailResponse>()
        .Produces(StatusCodes.Status404NotFound);

        admin.MapGet("/cms/sites/{siteId:guid}/pages", async (
            Guid siteId,
            AdminCmsQueryService cmsQueryService,
            CancellationToken cancellationToken) =>
        {
            var pages = await cmsQueryService.ListPagesAsync(siteId, cancellationToken);
            return Results.Ok(pages);
        })
        .WithName("ListAdminCmsPages")
        .Produces<IReadOnlyList<CmsPageSummaryResponse>>();

        admin.MapGet("/cms/pages/{pageId:guid}", async (
            Guid pageId,
            AdminCmsQueryService cmsQueryService,
            CancellationToken cancellationToken) =>
        {
            var page = await cmsQueryService.GetPageAsync(pageId, cancellationToken);
            return page is null ? Results.NotFound() : Results.Ok(page);
        })
        .WithName("GetAdminCmsPage")
        .Produces<CmsPageDetailResponse>()
        .Produces(StatusCodes.Status404NotFound);

        admin.MapPut("/cms/content-values/{id:guid}", async (
            Guid id,
            UpdateCmsContentValueRequest request,
            HttpRequest httpRequest,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var value = await db.CmsContentValues.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (value is null)
            {
                return Results.NotFound(new { message = "Champ CMS introuvable." });
            }

            var before = new
            {
                value.TextValue,
                value.JsonValue
            };

            value.SetText(request.TextValue);
            value.SetJson(request.JsonValue);

            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminCmsContentValueUpdated",
                nameof(CmsContentValue),
                value.Id,
                $"Champ CMS '{value.FieldKey}' mis a jour.",
                before,
                after: new { value.TextValue, value.JsonValue });

            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new CmsContentValueResponse(
                value.Id,
                value.SectionId,
                value.FieldKey,
                value.ValueType.ToString(),
                null,
                value.TextValue,
                value.JsonValue,
                value.MediaAssetId,
                null));
        })
        .WithName("UpdateAdminCmsContentValue");

        admin.MapGet("/cms/component-definitions", async (
            AdminCmsQueryService cmsQueryService,
            CancellationToken cancellationToken) =>
        {
            var components = await cmsQueryService.ListComponentDefinitionsAsync(cancellationToken);
            return Results.Ok(components);
        })
        .WithName("ListAdminCmsComponentDefinitions")
        .Produces<IReadOnlyList<CmsComponentDefinitionResponse>>();
        
        admin.MapGet("/company-applications", async (AdminQueryService queryService, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            try
            {
                var response = await queryService.ListCompanyApplicationsAsync(cancellationToken);
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
        
        admin.MapGet("/notifications", async (AdminQueryService queryService, CancellationToken cancellationToken) =>
        {
            var notifications = await queryService.ListNotificationsAsync(cancellationToken);
            return Results.Ok(notifications);
        })
        .WithName("ListNotificationOutboxMessages");
        
        admin.MapGet("/country-brandings/{countryCode}", async (
            string countryCode,
            AdminQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            var branding = await queryService.GetCountryBrandingAsync(countryCode, cancellationToken);
            return branding is null ? Results.NotFound() : Results.Ok(branding);
        })
        .WithName("GetAdminCountryBranding");
        
        admin.MapPut("/country-brandings/{countryCode}", async (
            string countryCode,
            UpdateCountryBrandingRequest request,
            HttpRequest httpRequest,
            AdminConfigurationService configurationService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await configurationService.UpdateCountryBrandingAsync(countryCode, request, cancellationToken);
            if (result.Status == AdminConfigurationUpdateStatus.ValidationFailed)
            {
                return Results.BadRequest(new { message = result.Message });
            }
        
            if (result.Status == AdminConfigurationUpdateStatus.NotFound)
            {
                return Results.NotFound(new { message = result.Message });
            }
        
            var branding = result.Branding!;
            var response = result.Response!;
            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminCountryBrandingUpdated",
                nameof(CountryBranding),
                branding.Id,
                $"Branding pays {response.CountryIsoCode} mis a jour.",
                result.Before,
                result.After);
            await db.SaveChangesAsync(cancellationToken);
        
            return Results.Ok(response);
        })
        .WithName("UpdateAdminCountryBranding");
        
        admin.MapGet("/company-applications/{id:guid}", async (
            Guid id,
            AdminQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            var application = await queryService.GetCompanyApplicationAsync(id, cancellationToken);
            return application is null ? Results.NotFound() : Results.Ok(application);
        })
        .WithName("GetCompanyApplication");
        
        admin.MapPut("/companies/{id:guid}/assignment-mode", async (
            Guid id,
            UpdateCompanyAssignmentModeRequest request,
            HttpRequest httpRequest,
            AdminConfigurationService configurationService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await configurationService.UpdateCompanyAssignmentModeAsync(id, request, cancellationToken);
            if (result.Status == AdminConfigurationUpdateStatus.NotFound)
            {
                return Results.NotFound();
            }
        
            if (result.Status == AdminConfigurationUpdateStatus.ValidationFailed)
            {
                return Results.BadRequest(new { message = result.Message });
            }
        
            var company = result.Company!;
            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminCompanyAssignmentModeUpdated",
                nameof(Company),
                company.Id,
                "Mode d'affectation entreprise modifie.",
                result.Before,
                result.After);
            await db.SaveChangesAsync(cancellationToken);
        
            return Results.Ok(result.Response);
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
            AddCompanyApplicationReviewAudit(
                db,
                httpRequest,
                "AdminCompanyApplicationApproved",
                "Demande entreprise validee.",
                application,
                result.PreviousStatus,
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
            AddCompanyApplicationReviewAudit(
                db,
                httpRequest,
                "AdminCompanyApplicationRejected",
                "Demande entreprise refusee.",
                application,
                result.PreviousStatus,
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
            AddCompanyApplicationReviewAudit(
                db,
                httpRequest,
                "AdminCompanyApplicationReopened",
                "Demande entreprise reouverte.",
                application,
                result.PreviousStatus,
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
            AddCompanyApplicationReviewAudit(
                db,
                httpRequest,
                "AdminCompanyApplicationMoreInformationRequested",
                "Complement demande sur un dossier entreprise.",
                application,
                result.PreviousStatus,
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

        admin.MapPost("/services/{serviceId:guid}/prestations", async (
            Guid serviceId,
            UpsertServicePrestationRequest request,
            HttpRequest httpRequest,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { message = "Le nom de la prestation est obligatoire." });
            }

            var service = await db.Services
                .Include(item => item.Prestations)
                .FirstOrDefaultAsync(item => item.Id == serviceId, cancellationToken);

            if (service is null)
            {
                return Results.NotFound(new { message = "Service introuvable." });
            }

            var before = new
            {
                service.Id,
                service.Name,
                Prestations = service.Prestations
                    .OrderBy(item => item.SortOrder)
                    .ThenBy(item => item.Name)
                    .Select(ToServicePrestationResponse)
                    .ToList()
            };

            var prestation = service.AddPrestation(request.Name, request.Description, request.SortOrder);
            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminServicePrestationUpserted",
                nameof(ServicePrestation),
                prestation.Id,
                $"Prestation '{prestation.Name}' rattachee au service '{service.Name}'.",
                before,
                after: ToServicePrestationResponse(prestation));
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToServicePrestationResponse(prestation));
        })
        .WithName("UpsertAdminServicePrestation");

        admin.MapPut("/service-prestations/{id:guid}", async (
            Guid id,
            UpsertServicePrestationRequest request,
            HttpRequest httpRequest,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { message = "Le nom de la prestation est obligatoire." });
            }

            var prestation = await db.ServicePrestations.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (prestation is null)
            {
                return Results.NotFound(new { message = "Prestation introuvable." });
            }

            var before = ToServicePrestationResponse(prestation);
            prestation.Rename(request.Name, request.Description);
            prestation.MoveTo(request.SortOrder);
            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminServicePrestationUpdated",
                nameof(ServicePrestation),
                prestation.Id,
                "Prestation de service modifiee.",
                before,
                after: ToServicePrestationResponse(prestation));
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToServicePrestationResponse(prestation));
        })
        .WithName("UpdateAdminServicePrestation");

        admin.MapPost("/service-prestations/{id:guid}/activate", async (
            Guid id,
            HttpRequest httpRequest,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var prestation = await db.ServicePrestations.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (prestation is null)
            {
                return Results.NotFound(new { message = "Prestation introuvable." });
            }

            var before = ToServicePrestationResponse(prestation);
            prestation.Activate();
            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminServicePrestationActivated",
                nameof(ServicePrestation),
                prestation.Id,
                "Prestation de service activee.",
                before,
                after: ToServicePrestationResponse(prestation));
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToServicePrestationResponse(prestation));
        })
        .WithName("ActivateAdminServicePrestation");

        admin.MapPost("/service-prestations/{id:guid}/deactivate", async (
            Guid id,
            HttpRequest httpRequest,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var prestation = await db.ServicePrestations.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (prestation is null)
            {
                return Results.NotFound(new { message = "Prestation introuvable." });
            }

            var before = ToServicePrestationResponse(prestation);
            prestation.Deactivate();
            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminServicePrestationDeactivated",
                nameof(ServicePrestation),
                prestation.Id,
                "Prestation de service desactivee.",
                before,
                after: ToServicePrestationResponse(prestation));
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToServicePrestationResponse(prestation));
        })
        .WithName("DeactivateAdminServicePrestation");
        
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
            AddCompanyApplicationDocumentReviewAudit(
                db,
                httpRequest,
                "AdminCompanyApplicationDocumentApproved",
                "Piece entreprise validee.",
                document,
                result.PreviousStatus,
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
            AddCompanyApplicationDocumentReviewAudit(
                db,
                httpRequest,
                "AdminCompanyApplicationDocumentRejected",
                "Piece entreprise refusee.",
                document,
                result.PreviousStatus,
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
            AddCompanyApplicationDocumentReviewAudit(
                db,
                httpRequest,
                "AdminCompanyApplicationDocumentReplacementRequested",
                "Remplacement de piece entreprise demande.",
                document,
                result.PreviousStatus,
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
            AddCompanyApplicationDocumentReviewAudit(
                db,
                httpRequest,
                "AdminCompanyApplicationDocumentReopened",
                "Piece entreprise reouverte.",
                document,
                result.PreviousStatus,
                after: new { document.ReviewStatus, document.ReviewNote });
            await db.SaveChangesAsync(cancellationToken);
        
            return Results.Ok(ToCompanyApplicationDocumentReviewResponse(document));
        })
        .WithName("ReopenCompanyApplicationDocument");
        
        admin.MapGet("/company-application-documents/{id:guid}/download", async (
            Guid id,
            AdminQueryService queryService,
            CompanyApplicationUploadService uploadService,
            CancellationToken cancellationToken) =>
        {
            var document = await queryService.GetCompanyApplicationDocumentFileAsync(id, cancellationToken);
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

    static ServicePrestationSummaryResponse ToServicePrestationResponse(ServicePrestation prestation)
    {
        return new ServicePrestationSummaryResponse(
            prestation.Id,
            prestation.Name,
            prestation.Description,
            prestation.SortOrder,
            prestation.IsActive);
    }

    static void AddCompanyApplicationReviewAudit(
        IAppDbContext db,
        HttpRequest request,
        string action,
        string summary,
        HomeService.Domain.Entities.CompanyApplication application,
        CompanyApplicationStatus? previousStatus,
        object? after)
    {
        AddAuditLog(
            db,
            request,
            AuditActor.Admin(),
            action,
            nameof(HomeService.Domain.Entities.CompanyApplication),
            application.Id,
            summary,
            before: new { Status = previousStatus },
            after);
    }

    static void AddCompanyApplicationDocumentReviewAudit(
        IAppDbContext db,
        HttpRequest request,
        string action,
        string summary,
        CompanyApplicationDocument document,
        DocumentReviewStatus? previousStatus,
        object? after)
    {
        AddAuditLog(
            db,
            request,
            AuditActor.Admin(),
            action,
            nameof(CompanyApplicationDocument),
            document.Id,
            summary,
            before: new { Status = previousStatus },
            after);
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
