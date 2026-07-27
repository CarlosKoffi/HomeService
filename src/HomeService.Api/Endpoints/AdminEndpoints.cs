using HomeService.Application.Admin;
using HomeService.Application.Auditing;
using HomeService.Application.Branding;
using HomeService.Application.Companies;
using HomeService.Application.Contact;
using HomeService.Application.Missions;
using HomeService.Application.Notifications;
using HomeService.Api.Auditing;
using HomeService.Contracts.Admin;
using HomeService.Contracts.Branding;
using HomeService.Contracts.Cms;
using HomeService.Contracts.Companies;
using HomeService.Contracts.Contact;
using HomeService.Contracts.Localization;
using HomeService.Contracts.Monitoring;
using HomeService.Contracts.Missions;
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

        admin.MapGet("/contact-requests", async (
            string? status,
            string? source,
            string? search,
            ContactRequestService contactRequestService,
            CancellationToken cancellationToken) =>
        {
            var requests = await contactRequestService.ListAdminAsync(status, source, search, cancellationToken);
            return Results.Ok(requests);
        })
        .WithName("ListAdminContactRequests")
        .Produces<IReadOnlyList<AdminContactRequestResponse>>();

        admin.MapPost("/contact-requests/{id:guid}/in-progress", async (
            Guid id,
            UpdateContactRequestStatusRequest request,
            HttpRequest httpRequest,
            ContactRequestService contactRequestService,
            CancellationToken cancellationToken) =>
        {
            var result = await contactRequestService.MarkInProgressAsync(
                id,
                request,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            if (!result.IsSuccess)
            {
                return Results.NotFound(new { message = result.Message });
            }

            return Results.Ok(result.Response);
        })
        .WithName("MarkContactRequestInProgress")
        .Produces<AdminContactRequestResponse>()
        .Produces(StatusCodes.Status404NotFound);

        admin.MapPost("/contact-requests/{id:guid}/close", async (
            Guid id,
            UpdateContactRequestStatusRequest request,
            HttpRequest httpRequest,
            ContactRequestService contactRequestService,
            CancellationToken cancellationToken) =>
        {
            var result = await contactRequestService.CloseAsync(
                id,
                request,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            if (!result.IsSuccess)
            {
                return Results.NotFound(new { message = result.Message });
            }

            return Results.Ok(result.Response);
        })
        .WithName("CloseContactRequest")
        .Produces<AdminContactRequestResponse>()
        .Produces(StatusCodes.Status404NotFound);

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
            AdminCmsContentManagementService contentService,
            CancellationToken cancellationToken) =>
        {
            var result = await contentService.UpdateContentValueAsync(
                id,
                request,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);

            if (result.Status == AdminCmsContentManagementStatus.NotFound)
            {
                return Results.NotFound(new { message = result.Message });
            }

            return Results.Ok(result.Response);
        })
        .WithName("UpdateAdminCmsContentValue");

        admin.MapPost("/cms/content-values/{id:guid}/media", async (
            Guid id,
            HttpRequest httpRequest,
            AdminCmsContentManagementService contentService,
            CmsMediaUploadService uploadService,
            CancellationToken cancellationToken) =>
        {
            if (!await contentService.ContentValueExistsAsync(id, cancellationToken))
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
                var mediaAsset = await uploadService.SaveAsync(file, cancellationToken);
                var mediaUrl = $"/api/cms/media/{mediaAsset.Id}";
                var result = await contentService.AttachMediaAsync(
                    id,
                    mediaAsset,
                    mediaUrl,
                    AuditActor.Admin(),
                    GetAuditRequestContext(httpRequest),
                    cancellationToken);

                return result.Status == AdminCmsContentManagementStatus.NotFound
                    ? Results.NotFound(new { message = result.Message })
                    : Results.Ok(result.Response);
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

            return Results.File(absolutePath, document.ContentType, enableRangeProcessing: true);
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

        admin.MapPost("/missions/{missionId:guid}/dispatch-offers", async (
            Guid missionId,
            bool? urgent,
            MissionDispatchService dispatchService,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatchService.CreateInitialOffersAsync(missionId, urgent ?? false, cancellationToken);
            if (!result.IsSuccess)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            return Results.Ok(result.Offers.Select(offer => new
            {
                offer.Id,
                offer.MissionId,
                offer.CompanyId,
                offer.Rank,
                offer.Score,
                offer.ScoreDetails,
                Status = offer.Status.ToString(),
                offer.ExpiresAt
            }));
        })
        .WithName("CreateAdminMissionDispatchOffers")
        .Produces(StatusCodes.Status400BadRequest);

        admin.MapPost("/missions/{missionId:guid}/mark-disputed", async (
            Guid missionId,
            CancelMissionRequest request,
            HttpRequest httpRequest,
            AdminQueryService queryService,
            AdminMissionDisputeService disputeService,
            CancellationToken cancellationToken) =>
        {
            var result = await disputeService.OpenAsync(
                missionId,
                request.Reason,
                request.Comment,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            if (result.Status == AdminMissionOperationStatus.NotFound)
            {
                return Results.NotFound(new { message = result.Message });
            }

            if (result.Status == AdminMissionOperationStatus.ValidationFailed)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            return Results.Ok(await queryService.ListMissionsAsync("Disputed", null, cancellationToken));
        })
        .WithName("MarkAdminMissionDisputed");

        admin.MapPost("/missions/{missionId:guid}/resolve-dispute", async (
            Guid missionId,
            ResolveMissionDisputeRequest request,
            HttpRequest httpRequest,
            AdminQueryService queryService,
            AdminMissionDisputeService disputeService,
            CancellationToken cancellationToken) =>
        {
            var result = await disputeService.ResolveAsync(
                missionId,
                request.Resolution,
                request.Note,
                request.RefundPercent,
                request.RefundAmount,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            if (result.Status == AdminMissionOperationStatus.NotFound)
            {
                return Results.NotFound(new { message = result.Message });
            }

            if (result.Status == AdminMissionOperationStatus.ValidationFailed)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            return Results.Ok(await queryService.GetMissionAsync(missionId, cancellationToken));
        })
        .WithName("ResolveAdminMissionDispute");

        admin.MapPost("/missions/{missionId:guid}/cancel", async (
            Guid missionId,
            CancelMissionRequest request,
            HttpRequest httpRequest,
            AdminQueryService queryService,
            AdminMissionOperationsService missionOperationsService,
            CancellationToken cancellationToken) =>
        {
            var result = await missionOperationsService.CancelAsync(
                missionId,
                request.Reason,
                request.Comment,
                request.CancellationFeeAmount,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            if (result.Status == AdminMissionOperationStatus.NotFound)
            {
                return Results.NotFound(new { message = result.Message });
            }

            if (result.Status == AdminMissionOperationStatus.ValidationFailed)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            return Results.Ok(await queryService.GetMissionAsync(missionId, cancellationToken));
        })
        .WithName("CancelAdminMission");

        admin.MapGet("/mission-settings", async (
            AdminMissionSettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await settingsService.GetAsync(cancellationToken));
        })
        .WithName("GetAdminMissionSettings")
        .Produces<AdminMissionSettingsResponse>();

        admin.MapPut("/mission-settings/commission-rules/{ruleId:guid}", async (
            Guid ruleId,
            UpdateAdminCommissionRuleRequest request,
            AdminMissionSettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            var result = await settingsService.UpdateCommissionRuleAsync(ruleId, request, cancellationToken);

            return result.Status switch
            {
                AdminMissionSettingsOperationStatus.NotFound => Results.NotFound(new { message = result.Message }),
                AdminMissionSettingsOperationStatus.ValidationFailed => Results.BadRequest(new { message = result.Message }),
                _ => Results.Ok(await settingsService.GetAsync(cancellationToken))
            };
        })
        .WithName("UpdateAdminCommissionRule")
        .Produces<AdminMissionSettingsResponse>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        admin.MapPut("/mission-settings/workflow-settings/{settingId:guid}", async (
            Guid settingId,
            UpdateAdminMissionWorkflowSettingRequest request,
            AdminMissionSettingsService settingsService,
            CancellationToken cancellationToken) =>
        {
            var result = await settingsService.UpdateWorkflowSettingAsync(settingId, request, cancellationToken);

            return result.Status switch
            {
                AdminMissionSettingsOperationStatus.NotFound => Results.NotFound(new { message = result.Message }),
                AdminMissionSettingsOperationStatus.ValidationFailed => Results.BadRequest(new { message = result.Message }),
                _ => Results.Ok(await settingsService.GetAsync(cancellationToken))
            };
        })
        .WithName("UpdateAdminMissionWorkflowSetting")
        .Produces<AdminMissionSettingsResponse>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

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
            CancellationToken cancellationToken) =>
        {
            var result = await providerOperationsService.ApproveAsync(
                providerId,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            if (result.Status == AdminProviderOperationStatus.NotFound)
            {
                return Results.NotFound(new { message = result.Message });
            }

            if (result.Status == AdminProviderOperationStatus.ValidationFailed)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            return Results.NoContent();
        })
        .WithName("ApproveAdminProvider")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        admin.MapPost("/providers/{providerId:guid}/suspend", async (
            Guid providerId,
            AdminProviderActionRequest? request,
            HttpRequest httpRequest,
            AdminProviderOperationsService providerOperationsService,
            CancellationToken cancellationToken) =>
        {
            var result = await providerOperationsService.SuspendAsync(
                providerId,
                request?.Note,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            if (result.Status == AdminProviderOperationStatus.NotFound)
            {
                return Results.NotFound(new { message = result.Message });
            }

            if (result.Status == AdminProviderOperationStatus.ValidationFailed)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            return Results.NoContent();
        })
        .WithName("SuspendAdminProvider")
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

        admin.MapGet("/company-portal-notifications", async (
            AdminQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            var notifications = await queryService.ListCompanyPortalNotificationsAsync(cancellationToken);
            return Results.Ok(notifications);
        })
        .WithName("ListAdminCompanyPortalNotifications")
        .Produces<IReadOnlyList<AdminCompanyPortalNotificationResponse>>();

        admin.MapGet("/notification-delivery-rules", async (
            AdminNotificationDeliveryRuleService ruleService,
            CancellationToken cancellationToken) =>
        {
            var rules = await ruleService.ListAsync(cancellationToken);
            return Results.Ok(rules);
        })
        .WithName("ListNotificationDeliveryRules")
        .Produces<IReadOnlyList<NotificationDeliveryRuleResponse>>();

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
            CancellationToken cancellationToken) =>
        {
            var result = await translationService.UpsertAsync(
                request,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            if (result.Status == AdminTranslationStatus.ValidationFailed)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            return Results.Ok(await translationService.ListAsync(request.Scope, request.Key, request.Language, cancellationToken));
        })
        .WithName("UpsertAdminTranslation");

        admin.MapPost("/access-control/roles", async (
            CreateAdminRoleRequest request,
            HttpRequest httpRequest,
            AdminAccessControlService accessControlService,
            CancellationToken cancellationToken) =>
        {
            var result = await accessControlService.CreateRoleAsync(
                request,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            var error = ToAdminAccessControlError(result);
            if (error is not null)
            {
                return error;
            }

            return Results.Ok(result.Snapshot);
        })
        .WithName("CreateAdminRole");

        admin.MapPut("/access-control/roles/{roleId:guid}/permissions", async (
            Guid roleId,
            UpdateAdminRolePermissionsRequest request,
            HttpRequest httpRequest,
            AdminAccessControlService accessControlService,
            CancellationToken cancellationToken) =>
        {
            var result = await accessControlService.UpdateRolePermissionsAsync(
                roleId,
                request,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            var error = ToAdminAccessControlError(result);
            if (error is not null)
            {
                return error;
            }

            return Results.Ok(result.Snapshot);
        })
        .WithName("UpdateAdminRolePermissions");

        admin.MapPost("/access-control/admins", async (
            CreateAdminUserRequest request,
            HttpRequest httpRequest,
            AdminAccessControlService accessControlService,
            CancellationToken cancellationToken) =>
        {
            var result = await accessControlService.CreateAdminUserAsync(
                request,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            var error = ToAdminAccessControlError(result);
            if (error is not null)
            {
                return error;
            }

            return Results.Ok(result.Snapshot);
        })
        .WithName("CreateAdminUser");

        admin.MapPut("/access-control/admins/{adminUserId:guid}/roles", async (
            Guid adminUserId,
            UpdateAdminUserRolesRequest request,
            HttpRequest httpRequest,
            AdminAccessControlService accessControlService,
            CancellationToken cancellationToken) =>
        {
            var result = await accessControlService.UpdateAdminUserRolesAsync(
                adminUserId,
                request,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            var error = ToAdminAccessControlError(result);
            if (error is not null)
            {
                return error;
            }

            return Results.Ok(result.Snapshot);
        })
        .WithName("UpdateAdminUserRoles");

        admin.MapPost("/access-control/admins/{adminUserId:guid}/deactivate", async (
            Guid adminUserId,
            HttpRequest httpRequest,
            AdminAccessControlService accessControlService,
            CancellationToken cancellationToken) =>
        {
            var result = await accessControlService.DeactivateAdminUserAsync(
                adminUserId,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            var error = ToAdminAccessControlError(result);
            if (error is not null)
            {
                return error;
            }

            return Results.Ok(result.Snapshot);
        })
        .WithName("DeactivateAdminUser");

        admin.MapPost("/access-control/admins/{adminUserId:guid}/reactivate", async (
            Guid adminUserId,
            HttpRequest httpRequest,
            AdminAccessControlService accessControlService,
            CancellationToken cancellationToken) =>
        {
            var result = await accessControlService.ReactivateAdminUserAsync(
                adminUserId,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            var error = ToAdminAccessControlError(result);
            if (error is not null)
            {
                return error;
            }

            return Results.Ok(result.Snapshot);
        })
        .WithName("ReactivateAdminUser");

        admin.MapPost("/notifications/{id:guid}/retry", async (
            Guid id,
            HttpRequest httpRequest,
            AdminNotificationService notificationService,
            CancellationToken cancellationToken) =>
        {
            var result = await notificationService.RetryAsync(
                id,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            var error = ToAdminNotificationActionError(result);
            if (error is not null)
            {
                return error;
            }

            return Results.Ok(result.Response);
        })
        .WithName("RetryNotificationOutboxMessage");

        admin.MapPost("/notifications/{id:guid}/cancel", async (
            Guid id,
            NotificationActionRequest request,
            HttpRequest httpRequest,
            AdminNotificationService notificationService,
            CancellationToken cancellationToken) =>
        {
            var result = await notificationService.CancelAsync(
                id,
                request.Reason,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            var error = ToAdminNotificationActionError(result);
            if (error is not null)
            {
                return error;
            }

            return Results.Ok(result.Response);
        })
        .WithName("CancelNotificationOutboxMessage");

        admin.MapPost("/notifications/{id:guid}/mark-sent", async (
            Guid id,
            HttpRequest httpRequest,
            AdminNotificationService notificationService,
            CancellationToken cancellationToken) =>
        {
            var result = await notificationService.MarkSentAsync(
                id,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            var error = ToAdminNotificationActionError(result);
            if (error is not null)
            {
                return error;
            }

            return Results.Ok(result.Response);
        })
        .WithName("MarkNotificationOutboxMessageSent");

        admin.MapPut("/notification-delivery-rules/{id:guid}", async (
            Guid id,
            UpdateNotificationDeliveryRuleRequest request,
            HttpRequest httpRequest,
            AdminNotificationDeliveryRuleService ruleService,
            CancellationToken cancellationToken) =>
        {
            var result = await ruleService.UpdateAsync(
                id,
                request,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            if (result.Status == AdminNotificationDeliveryRuleStatus.NotFound)
            {
                return Results.NotFound(new { message = result.Message });
            }

            if (result.Status == AdminNotificationDeliveryRuleStatus.ValidationFailed)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            return Results.Ok(result.Response);
        })
        .WithName("UpdateNotificationDeliveryRule")
        .Produces<NotificationDeliveryRuleResponse>();

        admin.MapGet("/notification-templates", async (
            AdminNotificationTemplateService templateService,
            CancellationToken cancellationToken) =>
        {
            var templates = await templateService.ListAsync(cancellationToken);
            return Results.Ok(templates);
        })
        .WithName("ListNotificationTemplates")
        .Produces<IReadOnlyList<NotificationTemplateResponse>>();

        admin.MapPost("/notification-templates", async (
            CreateNotificationTemplateRequest request,
            HttpRequest httpRequest,
            AdminNotificationTemplateService templateService,
            CancellationToken cancellationToken) =>
        {
            var result = await templateService.CreateAsync(
                request,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            if (result.Status == AdminNotificationTemplateStatus.ValidationFailed)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            if (result.Status == AdminNotificationTemplateStatus.Conflict)
            {
                return Results.Conflict(new { message = result.Message });
            }

            return Results.Created($"/api/admin/notification-templates/{result.Response!.Id:D}", result.Response);
        })
        .WithName("CreateNotificationTemplate")
        .Produces<NotificationTemplateResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict);

        admin.MapPut("/notification-templates/{id:guid}", async (
            Guid id,
            UpdateNotificationTemplateRequest request,
            HttpRequest httpRequest,
            AdminNotificationTemplateService templateService,
            CancellationToken cancellationToken) =>
        {
            var result = await templateService.UpdateAsync(
                id,
                request,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            if (result.Status == AdminNotificationTemplateStatus.NotFound)
            {
                return Results.NotFound(new { message = result.Message });
            }

            if (result.Status == AdminNotificationTemplateStatus.ValidationFailed)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            return Results.Ok(result.Response);
        })
        .WithName("UpdateNotificationTemplate")
        .Produces<NotificationTemplateResponse>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);
        
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
            CancellationToken cancellationToken) =>
        {
            var result = await configurationService.UpdateCountryBrandingAsync(
                countryCode,
                request,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            if (result.Status == AdminConfigurationUpdateStatus.ValidationFailed)
            {
                return Results.BadRequest(new { message = result.Message });
            }
        
            if (result.Status == AdminConfigurationUpdateStatus.NotFound)
            {
                return Results.NotFound(new { message = result.Message });
            }
        
            var response = result.Response!;
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
            CancellationToken cancellationToken) =>
        {
            var result = await configurationService.UpdateCompanyAssignmentModeAsync(
                id,
                request,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            if (result.Status == AdminConfigurationUpdateStatus.NotFound)
            {
                return Results.NotFound();
            }
        
            if (result.Status == AdminConfigurationUpdateStatus.ValidationFailed)
            {
                return Results.BadRequest(new { message = result.Message });
            }
        
            return Results.Ok(result.Response);
        })
        .WithName("UpdateCompanyAssignmentMode");

        admin.MapPut("/companies/{id:guid}/dispatch-settings", async (
            Guid id,
            UpdateAdminCompanyDispatchSettingsRequest request,
            HttpRequest httpRequest,
            AdminConfigurationService configurationService,
            CancellationToken cancellationToken) =>
        {
            var result = await configurationService.UpdateCompanyDispatchSettingsAsync(
                id,
                request,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            if (result.Status == AdminConfigurationUpdateStatus.NotFound)
            {
                return Results.NotFound(new { message = result.Message });
            }

            if (result.Status == AdminConfigurationUpdateStatus.ValidationFailed)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            var company = result.Company!;
            return Results.Ok(new
            {
                company.Id,
                company.MissionDispatchPriority,
                company.AcceptsUrgentMissions
            });
        })
        .WithName("UpdateCompanyDispatchSettings")
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        admin.MapPost("/companies/{id:guid}/suspend", async (
            Guid id,
            AdminCompanyActionRequest? request,
            HttpRequest httpRequest,
            AdminCompanyOperationsService companyOperationsService,
            CancellationToken cancellationToken) =>
        {
            var result = await companyOperationsService.SuspendAsync(
                id,
                request?.Note,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            if (result.Status == AdminCompanyOperationStatus.NotFound)
            {
                return Results.NotFound(new { message = result.Message });
            }

            if (result.Status == AdminCompanyOperationStatus.ValidationFailed)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            return Results.NoContent();
        })
        .WithName("SuspendAdminCompany")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        admin.MapPost("/companies/{id:guid}/reactivate", async (
            Guid id,
            AdminCompanyActionRequest? request,
            HttpRequest httpRequest,
            AdminCompanyOperationsService companyOperationsService,
            CancellationToken cancellationToken) =>
        {
            var result = await companyOperationsService.ReactivateAsync(
                id,
                request?.Note,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            if (result.Status == AdminCompanyOperationStatus.NotFound)
            {
                return Results.NotFound(new { message = result.Message });
            }

            if (result.Status == AdminCompanyOperationStatus.ValidationFailed)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            return Results.NoContent();
        })
        .WithName("ReactivateAdminCompany")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        admin.MapPost("/companies/{companyId:guid}/notifications/{notificationId:guid}/mark-read", async (
            Guid companyId,
            Guid notificationId,
            HttpRequest httpRequest,
            AdminCompanyNotificationService notificationService,
            CancellationToken cancellationToken) =>
        {
            var result = await notificationService.MarkReadAsync(
                companyId,
                notificationId,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            if (result.Status == AdminCompanyNotificationActionStatus.NotFound)
            {
                return Results.NotFound(new { message = result.Message });
            }

            return Results.NoContent();
        })
        .WithName("MarkAdminCompanyNotificationRead")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        admin.MapPost("/companies/{companyId:guid}/notifications/{notificationId:guid}/mark-unread", async (
            Guid companyId,
            Guid notificationId,
            HttpRequest httpRequest,
            AdminCompanyNotificationService notificationService,
            CancellationToken cancellationToken) =>
        {
            var result = await notificationService.MarkUnreadAsync(
                companyId,
                notificationId,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            if (result.Status == AdminCompanyNotificationActionStatus.NotFound)
            {
                return Results.NotFound(new { message = result.Message });
            }

            return Results.NoContent();
        })
        .WithName("MarkAdminCompanyNotificationUnread")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        admin.MapPost("/companies/{companyId:guid}/notifications/{notificationId:guid}/resend", async (
            Guid companyId,
            Guid notificationId,
            HttpRequest httpRequest,
            AdminCompanyNotificationService notificationService,
            CancellationToken cancellationToken) =>
        {
            var result = await notificationService.ResendAsync(
                companyId,
                notificationId,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            if (result.Status == AdminCompanyNotificationActionStatus.NotFound)
            {
                return Results.NotFound(new { message = result.Message });
            }

            return Results.NoContent();
        })
        .WithName("ResendAdminCompanyNotification")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);
        
        admin.MapPost("/company-applications/{id:guid}/approve", async (
            Guid id,
            HttpRequest httpRequest,
            AdminCompanyApplicationReviewService reviewService,
            CancellationToken cancellationToken) =>
        {
            var result = await reviewService.ApproveAsync(
                id,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            var error = ToAdminCompanyApplicationReviewError(result);
            if (error is not null)
            {
                return error;
            }
        
            var application = result.Application!;
            return Results.Ok(ToCompanyApplicationActionResponse(application));
        })
        .WithName("ApproveCompanyApplication");
        
        admin.MapPost("/company-applications/{id:guid}/reject", async (
            Guid id,
            CompanyApplicationReviewRequest request,
            HttpRequest httpRequest,
            AdminCompanyApplicationReviewService reviewService,
            CancellationToken cancellationToken) =>
        {
            var result = await reviewService.RejectAsync(
                id,
                request.Note,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            var error = ToAdminCompanyApplicationReviewError(result);
            if (error is not null)
            {
                return error;
            }
        
            var application = result.Application!;
            return Results.Ok(ToCompanyApplicationActionResponse(application));
        })
        .WithName("RejectCompanyApplication");
        
        admin.MapPost("/company-applications/{id:guid}/reopen", async (
            Guid id,
            CompanyApplicationReviewRequest request,
            HttpRequest httpRequest,
            AdminCompanyApplicationReviewService reviewService,
            CancellationToken cancellationToken) =>
        {
            var result = await reviewService.ReopenAsync(
                id,
                request.Note,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            var error = ToAdminCompanyApplicationReviewError(result);
            if (error is not null)
            {
                return error;
            }
        
            var application = result.Application!;
            return Results.Ok(ToCompanyApplicationActionResponse(application));
        })
        .WithName("ReopenCompanyApplication");
        
        admin.MapPost("/company-applications/{id:guid}/request-more-information", async (
            Guid id,
            CompanyApplicationReviewRequest request,
            HttpRequest httpRequest,
            AdminCompanyApplicationReviewService reviewService,
            CancellationToken cancellationToken) =>
        {
            var result = await reviewService.RequestMoreInformationAsync(
                id,
                request.Note,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            var error = ToAdminCompanyApplicationReviewError(result);
            if (error is not null)
            {
                return error;
            }
        
            var application = result.Application!;
            return Results.Ok(ToCompanyApplicationActionResponse(application));
        })
        .WithName("RequestCompanyApplicationMoreInformation");
        
        admin.MapPost("/company-applications/{id:guid}/activation-link", async (
            Guid id,
            HttpRequest httpRequest,
            CompanyActivationLinkGenerationService activationLinkService,
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
                    AuditActor.Admin(),
                    GetAuditRequestContext(httpRequest),
                    cancellationToken);
        
                if (result.Status == CompanyActivationLinkGenerationStatus.NotFound)
                {
                    return Results.NotFound(new { message = result.Message });
                }
        
                if (result.Status == CompanyActivationLinkGenerationStatus.InvalidStatus)
                {
                    return Results.BadRequest(new { message = result.Message });
                }

                if (result.Status == CompanyActivationLinkGenerationStatus.ConcurrencyConflict)
                {
                    return Results.Conflict(new { message = result.Message });
                }

                var response = result.Response!;
                return Results.Ok(response);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Activation link generation failed for company application {ApplicationId}.", id);
                if (exception is DbUpdateConcurrencyException)
                {
                    return Results.Conflict(new
                    {
                        message = "Le dossier a ete modifie pendant la generation du lien. Rechargez la fiche puis recommencez."
                    });
                }

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
            CancellationToken cancellationToken) =>
        {
            var result = await serviceProposalService.ReanalyseAsync(
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            if (!result.IsSuccess)
            {
                return ToCompanyServiceProposalActionError(result);
            }

            return Results.Ok(await serviceProposalService.ListAsync(cancellationToken));
        })
        .WithName("ReanalyseCompanyServiceProposals")
        .Produces<CompanyServiceProposalListResponse>();

        admin.MapPost("/company-service-proposals/{id:guid}/attach", async (
            Guid id,
            AttachCompanyServiceProposalRequest request,
            HttpRequest httpRequest,
            AdminCompanyServiceProposalService serviceProposalService,
            CancellationToken cancellationToken) =>
        {
            var result = await serviceProposalService.AttachAsync(
                id,
                request,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            if (!result.IsSuccess)
            {
                return ToCompanyServiceProposalActionError(result);
            }

            return Results.Ok(await serviceProposalService.ListAsync(cancellationToken));
        })
        .WithName("AttachCompanyServiceProposal")
        .Produces<CompanyServiceProposalListResponse>();

        admin.MapPost("/company-service-proposals/{id:guid}/create-prestation", async (
            Guid id,
            CreatePrestationFromCompanyServiceProposalRequest request,
            HttpRequest httpRequest,
            AdminCompanyServiceProposalService serviceProposalService,
            CancellationToken cancellationToken) =>
        {
            var result = await serviceProposalService.CreatePrestationAsync(
                id,
                request,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            if (!result.IsSuccess)
            {
                return ToCompanyServiceProposalActionError(result);
            }

            return Results.Ok(await serviceProposalService.ListAsync(cancellationToken));
        })
        .WithName("CreatePrestationFromCompanyServiceProposal")
        .Produces<CompanyServiceProposalListResponse>();

        admin.MapPost("/company-service-proposals/{id:guid}/create-service", async (
            Guid id,
            CreateServiceFromCompanyServiceProposalRequest request,
            HttpRequest httpRequest,
            AdminCompanyServiceProposalService serviceProposalService,
            CancellationToken cancellationToken) =>
        {
            var result = await serviceProposalService.CreateServiceAsync(
                id,
                request,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            if (!result.IsSuccess)
            {
                return ToCompanyServiceProposalActionError(result);
            }

            return Results.Ok(await serviceProposalService.ListAsync(cancellationToken));
        })
        .WithName("CreateServiceFromCompanyServiceProposal")
        .Produces<CompanyServiceProposalListResponse>();

        admin.MapPost("/services", async (
            UpsertServiceRequest request,
            HttpRequest httpRequest,
            AdminServiceCatalogManagementService catalogService,
            CancellationToken cancellationToken) =>
        {
            var result = await catalogService.CreateServiceAsync(
                request,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            var error = ToAdminServiceCatalogOperationError(result);
            if (error is not null)
            {
                return error;
            }

            return Results.Ok(result.Response);
        })
        .WithName("CreateAdminService");

        admin.MapPut("/services/{serviceId:guid}", async (
            Guid serviceId,
            UpsertServiceRequest request,
            HttpRequest httpRequest,
            AdminServiceCatalogManagementService catalogService,
            CancellationToken cancellationToken) =>
        {
            var result = await catalogService.UpdateServiceAsync(
                serviceId,
                request,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            var error = ToAdminServiceCatalogOperationError(result);
            if (error is not null)
            {
                return error;
            }

            return Results.Ok(result.Response);
        })
        .WithName("UpdateAdminService");

        admin.MapPost("/services/{serviceId:guid}/activate", async (
            Guid serviceId,
            HttpRequest httpRequest,
            AdminServiceCatalogManagementService catalogService,
            CancellationToken cancellationToken) =>
        {
            var result = await catalogService.ActivateServiceAsync(
                serviceId,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            var error = ToAdminServiceCatalogOperationError(result);
            if (error is not null)
            {
                return error;
            }

            return Results.Ok(result.Response);
        })
        .WithName("ActivateAdminService");

        admin.MapPost("/services/{serviceId:guid}/deactivate", async (
            Guid serviceId,
            HttpRequest httpRequest,
            AdminServiceCatalogManagementService catalogService,
            CancellationToken cancellationToken) =>
        {
            var result = await catalogService.DeactivateServiceAsync(
                serviceId,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            var error = ToAdminServiceCatalogOperationError(result);
            if (error is not null)
            {
                return error;
            }

            return Results.Ok(result.Response);
        })
        .WithName("DeactivateAdminService");

        admin.MapPost("/services/{serviceId:guid}/prestations", async (
            Guid serviceId,
            UpsertServicePrestationRequest request,
            HttpRequest httpRequest,
            AdminServiceCatalogManagementService catalogService,
            CancellationToken cancellationToken) =>
        {
            var result = await catalogService.CreatePrestationAsync(
                serviceId,
                request,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            var error = ToAdminServiceCatalogOperationError(result);
            if (error is not null)
            {
                return error;
            }

            return Results.Ok(result.Response);
        })
        .WithName("UpsertAdminServicePrestation");

        admin.MapPut("/service-prestations/{id:guid}", async (
            Guid id,
            UpsertServicePrestationRequest request,
            HttpRequest httpRequest,
            AdminServiceCatalogManagementService catalogService,
            CancellationToken cancellationToken) =>
        {
            var result = await catalogService.UpdatePrestationAsync(
                id,
                request,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            var error = ToAdminServiceCatalogOperationError(result);
            if (error is not null)
            {
                return error;
            }

            return Results.Ok(result.Response);
        })
        .WithName("UpdateAdminServicePrestation");

        admin.MapPost("/service-prestations/{id:guid}/activate", async (
            Guid id,
            HttpRequest httpRequest,
            AdminServiceCatalogManagementService catalogService,
            CancellationToken cancellationToken) =>
        {
            var result = await catalogService.ActivatePrestationAsync(
                id,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            var error = ToAdminServiceCatalogOperationError(result);
            if (error is not null)
            {
                return error;
            }

            return Results.Ok(result.Response);
        })
        .WithName("ActivateAdminServicePrestation");

        admin.MapPost("/service-prestations/{id:guid}/deactivate", async (
            Guid id,
            HttpRequest httpRequest,
            AdminServiceCatalogManagementService catalogService,
            CancellationToken cancellationToken) =>
        {
            var result = await catalogService.DeactivatePrestationAsync(
                id,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            var error = ToAdminServiceCatalogOperationError(result);
            if (error is not null)
            {
                return error;
            }

            return Results.Ok(result.Response);
        })
        .WithName("DeactivateAdminServicePrestation");
        
        admin.MapPost("/company-application-documents/{id:guid}/approve", async (
            Guid id,
            HttpRequest httpRequest,
            AdminCompanyApplicationDocumentReviewService documentReviewService,
            CancellationToken cancellationToken) =>
        {
            var result = await documentReviewService.ApproveAsync(
                id,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            var error = ToAdminCompanyApplicationDocumentReviewError(result);
            if (error is not null)
            {
                return error;
            }
        
            var document = result.Document!;
            return Results.Ok(ToCompanyApplicationDocumentReviewResponse(document));
        })
        .WithName("ApproveCompanyApplicationDocument");
        
        admin.MapPost("/company-application-documents/{id:guid}/reject", async (
            Guid id,
            CompanyApplicationDocumentReviewRequest request,
            HttpRequest httpRequest,
            AdminCompanyApplicationDocumentReviewService documentReviewService,
            CancellationToken cancellationToken) =>
        {
            var result = await documentReviewService.RejectAsync(
                id,
                request.Comment,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            var error = ToAdminCompanyApplicationDocumentReviewError(result);
            if (error is not null)
            {
                return error;
            }
        
            var document = result.Document!;
            return Results.Ok(ToCompanyApplicationDocumentReviewResponse(document));
        })
        .WithName("RejectCompanyApplicationDocument");
        
        admin.MapPost("/company-application-documents/{id:guid}/request-replacement", async (
            Guid id,
            CompanyApplicationDocumentReviewRequest request,
            HttpRequest httpRequest,
            AdminCompanyApplicationDocumentReviewService documentReviewService,
            CancellationToken cancellationToken) =>
        {
            var result = await documentReviewService.RequestReplacementAsync(
                id,
                request.Comment,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            var error = ToAdminCompanyApplicationDocumentReviewError(result);
            if (error is not null)
            {
                return error;
            }
        
            var document = result.Document!;
            return Results.Ok(ToCompanyApplicationDocumentReviewResponse(document));
        })
        .WithName("RequestCompanyApplicationDocumentReplacement");
        
        admin.MapPost("/company-application-documents/{id:guid}/reopen", async (
            Guid id,
            CompanyApplicationDocumentReviewRequest request,
            HttpRequest httpRequest,
            AdminCompanyApplicationDocumentReviewService documentReviewService,
            CancellationToken cancellationToken) =>
        {
            var result = await documentReviewService.ReopenAsync(
                id,
                request.Comment,
                AuditActor.Admin(),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            var error = ToAdminCompanyApplicationDocumentReviewError(result);
            if (error is not null)
            {
                return error;
            }
        
            var document = result.Document!;
            return Results.Ok(ToCompanyApplicationDocumentReviewResponse(document));
        })
        .WithName("ReopenCompanyApplicationDocument");
        
        admin.MapGet("/company-application-documents/{id:guid}/preview", async (
            Guid id,
            AdminQueryService queryService,
            CompanyApplicationUploadService uploadService,
            CancellationToken cancellationToken) =>
        {
            var document = await queryService.GetCompanyApplicationDocumentFileAsync(id, cancellationToken);
            if (document is null)
            {
                return Results.NotFound(new { message = "Document entreprise introuvable." });
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

            return Results.File(absolutePath, document.ContentType, enableRangeProcessing: true);
        })
        .WithName("PreviewCompanyApplicationDocument");

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

    static IResult? ToAdminServiceCatalogOperationError<T>(AdminServiceCatalogOperationResult<T> result)
    {
        return result.Status switch
        {
            AdminServiceCatalogOperationStatus.Ok => null,
            AdminServiceCatalogOperationStatus.NotFound => Results.NotFound(new { message = result.Message }),
            AdminServiceCatalogOperationStatus.ValidationFailed => Results.BadRequest(new { message = result.Message }),
            AdminServiceCatalogOperationStatus.Conflict => Results.Conflict(new { message = result.Message }),
            _ => Results.BadRequest(new { message = result.Message })
        };
    }
}
