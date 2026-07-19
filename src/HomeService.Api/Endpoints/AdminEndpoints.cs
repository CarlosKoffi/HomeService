using HomeService.Application.Abstractions;
using HomeService.Application.Admin;
using HomeService.Application.Auditing;
using HomeService.Application.Branding;
using HomeService.Application.Companies;
using HomeService.Application.Notifications;
using HomeService.Api.Auditing;
using HomeService.Contracts.Admin;
using HomeService.Contracts.Branding;
using HomeService.Contracts.Cms;
using HomeService.Contracts.Companies;
using HomeService.Contracts.Localization;
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
            string? contextType,
            Guid? contextId,
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
                take,
                contextType,
                contextId), cancellationToken);

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

        admin.MapPost("/cms/content-values/{id:guid}/media", async (
            Guid id,
            HttpRequest httpRequest,
            IAppDbContext db,
            CmsMediaUploadService uploadService,
            CancellationToken cancellationToken) =>
        {
            var value = await db.CmsContentValues.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (value is null)
            {
                return Results.NotFound(new { message = "Champ CMS introuvable." });
            }

            if (!httpRequest.HasFormContentType)
            {
                return Results.BadRequest(new { message = "Le formulaire doit contenir une image." });
            }

            var form = await httpRequest.ReadFormAsync(cancellationToken);
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is null)
            {
                return Results.BadRequest(new { message = "Aucune image CMS recue." });
            }

            try
            {
                var before = new
                {
                    value.TextValue,
                    value.MediaAssetId
                };

                var mediaAsset = await uploadService.SaveAsync(file, cancellationToken);
                db.CmsMediaAssets.Add(mediaAsset);

                var mediaUrl = $"/api/cms/media/{mediaAsset.Id}";
                value.AttachMedia(mediaAsset.Id, mediaUrl);

                AddAuditLog(
                    db,
                    httpRequest,
                    AuditActor.Admin(),
                    "AdminCmsMediaUploaded",
                    nameof(CmsContentValue),
                    value.Id,
                    $"Image CMS '{value.FieldKey}' remplacee.",
                    before,
                    after: new { value.TextValue, value.MediaAssetId, mediaAsset.FileName, mediaAsset.SizeInBytes });

                await db.SaveChangesAsync(cancellationToken);

                return Results.Ok(new CmsMediaUploadResponse(
                    mediaAsset.Id,
                    mediaAsset.FileName,
                    mediaUrl,
                    mediaAsset.ContentType,
                    mediaAsset.SizeInBytes));
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { message = exception.Message });
            }
        })
        .DisableAntiforgery()
        .WithName("UploadAdminCmsMedia")
        .Produces<CmsMediaUploadResponse>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        admin.MapGet("/cms/component-definitions", async (
            AdminCmsQueryService cmsQueryService,
            CancellationToken cancellationToken) =>
        {
            var components = await cmsQueryService.ListComponentDefinitionsAsync(cancellationToken);
            return Results.Ok(components);
        })
        .WithName("ListAdminCmsComponentDefinitions")
        .Produces<IReadOnlyList<CmsComponentDefinitionResponse>>();

        admin.MapGet("/companies", async (
            string? status,
            string? search,
            AdminQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            var response = await queryService.ListCompaniesAsync(status, search, cancellationToken);
            return Results.Ok(response);
        })
        .WithName("ListAdminCompanies")
        .Produces<AdminCompanyListResponse>();

        admin.MapGet("/companies/{companyId:guid}", async (
            Guid companyId,
            AdminQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            var response = await queryService.GetCompanyAsync(companyId, cancellationToken);
            return response is null
                ? Results.NotFound(new { message = "Entreprise introuvable." })
                : Results.Ok(response);
        })
        .WithName("GetAdminCompany")
        .Produces<AdminCompanyDetailResponse>();

        admin.MapGet("/provider-documents/{id:guid}/preview", async (
            Guid id,
            AdminQueryService queryService,
            CompanyProviderUploadService uploadService,
            CancellationToken cancellationToken) =>
        {
            var document = await queryService.GetProviderDocumentFileAsync(id, cancellationToken);
            if (document is null)
            {
                return Results.NotFound(new { message = "Document prestataire introuvable." });
            }

            var absolutePath = uploadService.GetAbsolutePath(document.StoragePath);
            if (!File.Exists(absolutePath))
            {
                return Results.NotFound(new { message = "Le fichier prestataire n'existe plus sur le serveur." });
            }

            return Results.File(absolutePath, document.ContentType, document.OriginalFileName, enableRangeProcessing: true);
        })
        .WithName("PreviewAdminProviderDocument");

        admin.MapGet("/missions", async (
            string? status,
            string? search,
            AdminQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            var response = await queryService.ListMissionsAsync(status, search, cancellationToken);
            return Results.Ok(response);
        })
        .WithName("ListAdminMissions")
        .Produces<AdminMissionListResponse>();

        admin.MapGet("/missions/{missionId:guid}", async (
            Guid missionId,
            AdminQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            var response = await queryService.GetMissionAsync(missionId, cancellationToken);
            return response is null
                ? Results.NotFound(new { message = "Mission introuvable." })
                : Results.Ok(response);
        })
        .WithName("GetAdminMission")
        .Produces<AdminMissionDetailResponse>()
        .Produces(StatusCodes.Status404NotFound);

        admin.MapPost("/missions/{missionId:guid}/mark-disputed", async (
            Guid missionId,
            AdminMissionActionRequest request,
            HttpRequest httpRequest,
            AdminQueryService queryService,
            AdminMissionOperationsService missionOperationsService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await missionOperationsService.MarkDisputedAsync(missionId, request.Note, cancellationToken);
            if (result.Status == AdminMissionOperationStatus.NotFound)
            {
                return Results.NotFound(new { message = result.Message });
            }

            if (result.Status == AdminMissionOperationStatus.ValidationFailed)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminMissionMarkedDisputed",
                nameof(Mission),
                missionId,
                $"Mission marquee en litige. Note: {result.Note}",
                before: new { Status = result.PreviousStatus?.ToString() },
                after: new { Status = result.Mission!.Status.ToString(), result.Note });
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(await queryService.ListMissionsAsync("Disputed", null, cancellationToken));
        })
        .WithName("MarkAdminMissionDisputed");

        admin.MapGet("/providers", async (
            string? status,
            string? employmentType,
            string? search,
            AdminQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            var response = await queryService.ListProvidersAsync(status, employmentType, search, cancellationToken);
            return Results.Ok(response);
        })
        .WithName("ListAdminProviders")
        .Produces<AdminProviderListResponse>();

        admin.MapGet("/providers/{providerId:guid}", async (
            Guid providerId,
            AdminQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            var response = await queryService.GetProviderAsync(providerId, cancellationToken);
            return response is null
                ? Results.NotFound(new { message = "Prestataire introuvable." })
                : Results.Ok(response);
        })
        .WithName("GetAdminProvider")
        .Produces<AdminProviderDetailResponse>()
        .Produces(StatusCodes.Status404NotFound);

        admin.MapPost("/providers/{providerId:guid}/approve", async (
            Guid providerId,
            HttpRequest httpRequest,
            AdminProviderOperationsService providerOperationsService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await providerOperationsService.ApproveAsync(providerId, cancellationToken);
            if (result.Status == AdminProviderOperationStatus.NotFound)
            {
                return Results.NotFound(new { message = result.Message });
            }

            if (result.Status == AdminProviderOperationStatus.ValidationFailed)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            var provider = result.Provider!;
            db.AuditLogEntries.Add(AuditLogFactory.Create(
                AuditActor.Admin(),
                "AdminProviderApproved",
                nameof(ProviderProfile),
                provider.Id,
                "Prestataire valide par l'administration.",
                HttpAuditContextFactory.Create(httpRequest),
                before: new { Status = result.PreviousStatus?.ToString() },
                after: new { provider.Status, provider.CompanyId }));
            await db.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        })
        .WithName("ApproveAdminProvider")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        admin.MapGet("/payments", async (
            string? period,
            string? paymentStatus,
            string? paymentMethod,
            string? search,
            AdminQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            var response = await queryService.ListPaymentsAsync(period, paymentStatus, paymentMethod, search, cancellationToken);
            return Results.Ok(response);
        })
        .WithName("ListAdminPayments")
        .Produces<AdminPaymentListResponse>();
        
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

        admin.MapGet("/access-control", async (
            AdminQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            var snapshot = await queryService.GetAccessSnapshotAsync(cancellationToken);
            return Results.Ok(snapshot);
        })
        .WithName("GetAdminAccessControl")
        .Produces<AdminAccessSnapshotResponse>();

        admin.MapGet("/translations", async (
            string? scope,
            string? search,
            string? language,
            AdminTranslationService translationService,
            CancellationToken cancellationToken) =>
        {
            var response = await translationService.ListAsync(scope, search, language, cancellationToken);
            return Results.Ok(response);
        })
        .WithName("ListAdminTranslations");

        admin.MapPost("/translations", async (
            UpsertAdminTranslationRequest request,
            HttpRequest httpRequest,
            AdminTranslationService translationService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await translationService.UpsertAsync(request, cancellationToken);
            if (result.Status == AdminTranslationStatus.ValidationFailed)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminTranslationSaved",
                "TranslationKey",
                null,
                $"Traduction sauvegardee: {request.Key}.",
                after: request);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(await translationService.ListAsync(request.Scope, request.Key, request.Language, cancellationToken));
        })
        .WithName("UpsertAdminTranslation");

        admin.MapPost("/access-control/roles", async (
            CreateAdminRoleRequest request,
            HttpRequest httpRequest,
            AdminAccessControlService accessControlService,
            AdminQueryService queryService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await accessControlService.CreateRoleAsync(request, cancellationToken);
            var error = ToAdminAccessControlError(result);
            if (error is not null)
            {
                return error;
            }

            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminRoleCreated",
                "AdminRole",
                null,
                $"Role admin cree: {request.Name}.",
                after: request);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(await queryService.GetAccessSnapshotAsync(cancellationToken));
        })
        .WithName("CreateAdminRole");

        admin.MapPut("/access-control/roles/{roleId:guid}/permissions", async (
            Guid roleId,
            UpdateAdminRolePermissionsRequest request,
            HttpRequest httpRequest,
            AdminAccessControlService accessControlService,
            AdminQueryService queryService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await accessControlService.UpdateRolePermissionsAsync(roleId, request, cancellationToken);
            var error = ToAdminAccessControlError(result);
            if (error is not null)
            {
                return error;
            }

            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminRolePermissionsUpdated",
                "AdminRole",
                roleId,
                "Permissions du role admin modifiees.",
                after: request);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(await queryService.GetAccessSnapshotAsync(cancellationToken));
        })
        .WithName("UpdateAdminRolePermissions");

        admin.MapPost("/access-control/admins", async (
            CreateAdminUserRequest request,
            HttpRequest httpRequest,
            AdminAccessControlService accessControlService,
            AdminQueryService queryService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await accessControlService.CreateAdminUserAsync(request, cancellationToken);
            var error = ToAdminAccessControlError(result);
            if (error is not null)
            {
                return error;
            }

            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminUserInvited",
                "AdminUser",
                null,
                $"Admin invite: {request.Email}.",
                after: request);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(await queryService.GetAccessSnapshotAsync(cancellationToken));
        })
        .WithName("CreateAdminUser");

        admin.MapPut("/access-control/admins/{adminUserId:guid}/roles", async (
            Guid adminUserId,
            UpdateAdminUserRolesRequest request,
            HttpRequest httpRequest,
            AdminAccessControlService accessControlService,
            AdminQueryService queryService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await accessControlService.UpdateAdminUserRolesAsync(adminUserId, request, cancellationToken);
            var error = ToAdminAccessControlError(result);
            if (error is not null)
            {
                return error;
            }

            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminUserRolesUpdated",
                "AdminUser",
                adminUserId,
                "Roles de l'admin modifies.",
                after: request);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(await queryService.GetAccessSnapshotAsync(cancellationToken));
        })
        .WithName("UpdateAdminUserRoles");

        admin.MapPost("/access-control/admins/{adminUserId:guid}/deactivate", async (
            Guid adminUserId,
            HttpRequest httpRequest,
            AdminAccessControlService accessControlService,
            AdminQueryService queryService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await accessControlService.DeactivateAdminUserAsync(adminUserId, cancellationToken);
            var error = ToAdminAccessControlError(result);
            if (error is not null)
            {
                return error;
            }

            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminUserDeactivated",
                "AdminUser",
                adminUserId,
                "Admin desactive.");
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(await queryService.GetAccessSnapshotAsync(cancellationToken));
        })
        .WithName("DeactivateAdminUser");

        admin.MapPost("/notifications/{id:guid}/retry", async (
            Guid id,
            HttpRequest httpRequest,
            AdminNotificationService notificationService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await notificationService.RetryAsync(id, cancellationToken);
            var error = ToAdminNotificationActionError(result);
            if (error is not null)
            {
                return error;
            }

            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminNotificationRetried",
                "NotificationOutboxMessage",
                id,
                "Notification relancee.",
                after: result.Response);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(result.Response);
        })
        .WithName("RetryNotificationOutboxMessage");

        admin.MapPost("/notifications/{id:guid}/cancel", async (
            Guid id,
            NotificationActionRequest request,
            HttpRequest httpRequest,
            AdminNotificationService notificationService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await notificationService.CancelAsync(id, request.Reason, cancellationToken);
            var error = ToAdminNotificationActionError(result);
            if (error is not null)
            {
                return error;
            }

            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminNotificationCancelled",
                "NotificationOutboxMessage",
                id,
                "Notification annulee.",
                after: result.Response);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(result.Response);
        })
        .WithName("CancelNotificationOutboxMessage");

        admin.MapPost("/notifications/{id:guid}/mark-sent", async (
            Guid id,
            HttpRequest httpRequest,
            AdminNotificationService notificationService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await notificationService.MarkSentAsync(id, cancellationToken);
            var error = ToAdminNotificationActionError(result);
            if (error is not null)
            {
                return error;
            }

            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminNotificationMarkedSent",
                "NotificationOutboxMessage",
                id,
                "Notification marquee envoyee.",
                after: result.Response);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(result.Response);
        })
        .WithName("MarkNotificationOutboxMessageSent");
        
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

        admin.MapGet("/company-service-proposals", async (
            AdminCompanyServiceProposalService serviceProposalService,
            CancellationToken cancellationToken) =>
        {
            var result = await serviceProposalService.ListAsync(cancellationToken);
            return Results.Ok(result);
        })
        .WithName("ListCompanyServiceProposals")
        .Produces<CompanyServiceProposalListResponse>();

        admin.MapGet("/service-insights", async (
            AdminServiceCatalogInsightsService insightsService,
            CancellationToken cancellationToken) =>
        {
            var result = await insightsService.GetAsync(cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetAdminServiceCatalogInsights")
        .Produces<ServiceCatalogInsightListResponse>();

        admin.MapPost("/company-service-proposals/reanalyse", async (
            HttpRequest httpRequest,
            AdminCompanyServiceProposalService serviceProposalService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await serviceProposalService.ReanalyseAsync(cancellationToken);
            if (!result.IsSuccess)
            {
                return ToCompanyServiceProposalActionError(result);
            }

            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminCompanyServiceProposalsReanalysed",
                nameof(CompanyApplicationService),
                null,
                result.Message,
                before: null,
                after: null);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(await serviceProposalService.ListAsync(cancellationToken));
        })
        .WithName("ReanalyseCompanyServiceProposals")
        .Produces<CompanyServiceProposalListResponse>();

        admin.MapPost("/company-service-proposals/{id:guid}/attach", async (
            Guid id,
            AttachCompanyServiceProposalRequest request,
            HttpRequest httpRequest,
            AdminCompanyServiceProposalService serviceProposalService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await serviceProposalService.AttachAsync(id, request, cancellationToken);
            if (!result.IsSuccess)
            {
                return ToCompanyServiceProposalActionError(result);
            }

            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminCompanyServiceProposalAttached",
                nameof(CompanyApplicationService),
                id,
                result.Message,
                before: null,
                after: request);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(await serviceProposalService.ListAsync(cancellationToken));
        })
        .WithName("AttachCompanyServiceProposal")
        .Produces<CompanyServiceProposalListResponse>();

        admin.MapPost("/company-service-proposals/{id:guid}/create-prestation", async (
            Guid id,
            CreatePrestationFromCompanyServiceProposalRequest request,
            HttpRequest httpRequest,
            AdminCompanyServiceProposalService serviceProposalService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await serviceProposalService.CreatePrestationAsync(id, request, cancellationToken);
            if (!result.IsSuccess)
            {
                return ToCompanyServiceProposalActionError(result);
            }

            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminCompanyServiceProposalPrestationCreated",
                nameof(CompanyApplicationService),
                id,
                result.Message,
                before: null,
                after: request);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(await serviceProposalService.ListAsync(cancellationToken));
        })
        .WithName("CreatePrestationFromCompanyServiceProposal")
        .Produces<CompanyServiceProposalListResponse>();

        admin.MapPost("/company-service-proposals/{id:guid}/create-service", async (
            Guid id,
            CreateServiceFromCompanyServiceProposalRequest request,
            HttpRequest httpRequest,
            AdminCompanyServiceProposalService serviceProposalService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await serviceProposalService.CreateServiceAsync(id, request, cancellationToken);
            if (!result.IsSuccess)
            {
                return ToCompanyServiceProposalActionError(result);
            }

            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminCompanyServiceProposalServiceCreated",
                nameof(CompanyApplicationService),
                id,
                result.Message,
                before: null,
                after: request);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(await serviceProposalService.ListAsync(cancellationToken));
        })
        .WithName("CreateServiceFromCompanyServiceProposal")
        .Produces<CompanyServiceProposalListResponse>();

        admin.MapPost("/services", async (
            UpsertServiceRequest request,
            HttpRequest httpRequest,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { message = "Le nom du service est obligatoire." });
            }

            var normalizedName = request.Name.Trim().ToLowerInvariant();
            var existing = await db.Services
                .Include(service => service.Prestations)
                .FirstOrDefaultAsync(service => service.NormalizedName == normalizedName, cancellationToken);

            if (existing is not null)
            {
                return Results.Conflict(new { message = "Un service avec ce nom existe deja." });
            }

            var service = new Service(request.Name, request.Description, createdByCompanyId: null);
            service.UpdateDetails(request.Name, request.Description, request.IconName);
            service.UpdatePriceRange(
                request.PriceMinAmount ?? request.NormalPriceAmount,
                request.PriceMaxAmount ?? request.PremiumPriceAmount,
                request.Currency);
            service.Approve();
            db.Services.Add(service);

            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminServiceCreated",
                nameof(Service),
                service.Id,
                $"Service '{service.Name}' cree dans le catalogue.",
                before: null,
                after: ToServiceResponse(service));
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToServiceResponse(service));
        })
        .WithName("CreateAdminService");

        admin.MapPut("/services/{serviceId:guid}", async (
            Guid serviceId,
            UpsertServiceRequest request,
            HttpRequest httpRequest,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { message = "Le nom du service est obligatoire." });
            }

            var normalizedName = request.Name.Trim().ToLowerInvariant();
            var duplicate = await db.Services.AnyAsync(
                service => service.Id != serviceId && service.NormalizedName == normalizedName,
                cancellationToken);
            if (duplicate)
            {
                return Results.Conflict(new { message = "Un autre service utilise deja ce nom." });
            }

            var service = await db.Services
                .Include(item => item.Prestations)
                .FirstOrDefaultAsync(item => item.Id == serviceId, cancellationToken);
            if (service is null)
            {
                return Results.NotFound(new { message = "Service introuvable." });
            }

            var before = ToServiceResponse(service);
            service.UpdateDetails(request.Name, request.Description, request.IconName);
            service.UpdatePriceRange(
                request.PriceMinAmount ?? request.NormalPriceAmount,
                request.PriceMaxAmount ?? request.PremiumPriceAmount,
                request.Currency);

            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminServiceUpdated",
                nameof(Service),
                service.Id,
                "Service modifie dans le catalogue.",
                before,
                after: ToServiceResponse(service));
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToServiceResponse(service));
        })
        .WithName("UpdateAdminService");

        admin.MapPost("/services/{serviceId:guid}/activate", async (
            Guid serviceId,
            HttpRequest httpRequest,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var service = await db.Services
                .Include(item => item.Prestations)
                .FirstOrDefaultAsync(item => item.Id == serviceId, cancellationToken);
            if (service is null)
            {
                return Results.NotFound(new { message = "Service introuvable." });
            }

            var before = ToServiceResponse(service);
            service.Activate();
            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminServiceActivated",
                nameof(Service),
                service.Id,
                "Service active dans le catalogue.",
                before,
                after: ToServiceResponse(service));
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToServiceResponse(service));
        })
        .WithName("ActivateAdminService");

        admin.MapPost("/services/{serviceId:guid}/deactivate", async (
            Guid serviceId,
            HttpRequest httpRequest,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var service = await db.Services
                .Include(item => item.Prestations)
                .FirstOrDefaultAsync(item => item.Id == serviceId, cancellationToken);
            if (service is null)
            {
                return Results.NotFound(new { message = "Service introuvable." });
            }

            var before = ToServiceResponse(service);
            service.Deactivate();
            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Admin(),
                "AdminServiceDeactivated",
                nameof(Service),
                service.Id,
                "Service desactive dans le catalogue.",
                before,
                after: ToServiceResponse(service));
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToServiceResponse(service));
        })
        .WithName("DeactivateAdminService");

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

            var prestation = service.AddPrestation(
                request.Name,
                request.Description,
                request.SortOrder,
                request.PriceMinAmount ?? request.NormalPriceAmount,
                request.PriceMaxAmount ?? request.PremiumPriceAmount,
                request.Currency);
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
            prestation.UpdatePriceRange(
                request.PriceMinAmount ?? request.NormalPriceAmount,
                request.PriceMaxAmount ?? request.PremiumPriceAmount,
                request.Currency);
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

    static ServiceSummaryResponse ToServiceResponse(Service service)
    {
        return new ServiceSummaryResponse(
            service.Id,
            service.Name,
            service.Description,
            service.IconName,
            service.Status.ToString(),
            service.IsActive,
            service.NormalPriceAmount,
            service.PremiumPriceAmount,
            service.Currency,
            service.Prestations
                .OrderBy(prestation => prestation.SortOrder)
                .ThenBy(prestation => prestation.Name)
                .Select(ToServicePrestationResponse)
                .ToList(),
            service.PriceMinAmount,
            service.PriceMaxAmount);
    }

    static ServicePrestationSummaryResponse ToServicePrestationResponse(ServicePrestation prestation)
    {
        return new ServicePrestationSummaryResponse(
            prestation.Id,
            prestation.Name,
            prestation.Description,
            prestation.SortOrder,
            prestation.NormalPriceAmount,
            prestation.PremiumPriceAmount,
            prestation.Currency,
            prestation.IsActive,
            prestation.PriceMinAmount,
            prestation.PriceMaxAmount);
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

    static IResult? ToAdminNotificationActionError(AdminNotificationActionResult result)
    {
        return result.Status switch
        {
            AdminNotificationActionStatus.Ok => null,
            AdminNotificationActionStatus.NotFound => Results.NotFound(new { message = result.Message }),
            AdminNotificationActionStatus.InvalidTransition => Results.BadRequest(new { message = result.Message }),
            _ => Results.BadRequest(new { message = result.Message ?? "Action notification impossible." })
        };
    }

    static IResult? ToAdminAccessControlError(AdminAccessControlResult result)
    {
        return result.Status switch
        {
            AdminAccessControlStatus.Ok => null,
            AdminAccessControlStatus.NotFound => Results.NotFound(new { message = result.Message }),
            AdminAccessControlStatus.ValidationFailed => Results.BadRequest(new { message = result.Message }),
            _ => Results.BadRequest(new { message = result.Message ?? "Action acces admin impossible." })
        };
    }

    static IResult ToCompanyServiceProposalActionError(CompanyServiceProposalActionResult result)
    {
        return result.Status switch
        {
            CompanyServiceProposalActionStatus.NotFound => Results.NotFound(new { message = result.Message }),
            CompanyServiceProposalActionStatus.ValidationFailed => Results.BadRequest(new { message = result.Message }),
            _ => Results.BadRequest(new { message = result.Message })
        };
    }
}
