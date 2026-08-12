using HomeService.Application.Admin;
using HomeService.Application.Auditing;
using HomeService.Application.Branding;
using HomeService.Application.Companies;
using HomeService.Application.Abstractions;
using HomeService.Application.CompanyPortal;
using HomeService.Application.Contact;
using HomeService.Application.Missions;
using HomeService.Application.Notifications;
using HomeService.Application.Quality;
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
using HomeService.Contracts.Clients;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Api.Endpoints;

public static class AdminEndpoints
{
    private const string CurrentAdminUserItemKey = "CurrentAdminUser";

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin");
        admin.AddEndpointFilter(AdminSessionEndpointFilterAsync);

        admin.MapPost("/auth/login", async (
            AdminLoginRequest request,
            AdminAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var result = await authService.LoginAsync(request, cancellationToken);
            return result.IsSuccess && result.Response is not null
                ? Results.Ok(result.Response)
                : Results.Json(
                    new { message = result.Message ?? "Connexion refusée." },
                    statusCode: StatusCodes.Status401Unauthorized);
        })
        .WithName("LoginAdmin")
        .AllowAnonymous()
        .RequireRateLimiting(AuthenticationRateLimitingExtensions.PolicyName)
        .Produces<AdminLoginResponse>()
        .Produces(StatusCodes.Status401Unauthorized);

        admin.MapGet("/auth/me", async (
            HttpRequest request,
            AdminAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var currentUser = await authService.GetCurrentUserAsync(GetAdminSessionToken(request), cancellationToken);
            return currentUser is null ? Results.Unauthorized() : Results.Ok(currentUser);
        })
        .WithName("GetCurrentAdmin")
        .AllowAnonymous()
        .Produces<AdminCurrentUserResponse>()
        .Produces(StatusCodes.Status401Unauthorized);

        admin.MapPost("/auth/logout", async (
            HttpRequest request,
            AdminAuthService authService,
            CancellationToken cancellationToken) =>
        {
            await authService.LogoutAsync(GetAdminSessionToken(request), cancellationToken);
            return Results.NoContent();
        })
        .WithName("LogoutAdmin")
        .AllowAnonymous()
        .Produces(StatusCodes.Status204NoContent);

        admin.MapGet("/auth/mfa", async (
            HttpRequest request,
            AdminAuthService authService,
            AdminMfaService mfaService,
            CancellationToken cancellationToken) =>
        {
            var currentUser = await authService.GetCurrentUserAsync(GetAdminSessionToken(request), cancellationToken);
            if (currentUser is null)
            {
                return Results.Unauthorized();
            }

            var status = await mfaService.GetStatusAsync(currentUser.Id, cancellationToken);
            return status is null ? Results.NotFound() : Results.Ok(status);
        })
        .WithName("GetAdminMfaStatus")
        .AllowAnonymous()
        .Produces<AdminMfaStatusResponse>()
        .Produces(StatusCodes.Status401Unauthorized);

        admin.MapPost("/auth/mfa/setup", async (
            HttpRequest request,
            AdminAuthService authService,
            AdminMfaService mfaService,
            CancellationToken cancellationToken) =>
        {
            var currentUser = await authService.GetCurrentUserAsync(GetAdminSessionToken(request), cancellationToken);
            if (currentUser is null)
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(await mfaService.BeginEnrollmentAsync(currentUser.Id, cancellationToken));
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { message = exception.Message });
            }
        })
        .WithName("BeginAdminMfaEnrollment")
        .AllowAnonymous()
        .RequireRateLimiting(AuthenticationRateLimitingExtensions.PolicyName)
        .Produces<AdminMfaEnrollmentResponse>();

        admin.MapPost("/auth/mfa/activate", async (
            AdminMfaCodeRequest mfaRequest,
            HttpRequest request,
            AdminAuthService authService,
            AdminMfaService mfaService,
            CancellationToken cancellationToken) =>
        {
            var currentUser = await authService.GetCurrentUserAsync(GetAdminSessionToken(request), cancellationToken);
            if (currentUser is null)
            {
                return Results.Unauthorized();
            }

            try
            {
                return Results.Ok(await mfaService.ActivateAsync(currentUser.Id, mfaRequest.Code, cancellationToken));
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { message = exception.Message });
            }
        })
        .WithName("ActivateAdminMfa")
        .AllowAnonymous()
        .RequireRateLimiting(AuthenticationRateLimitingExtensions.PolicyName)
        .Produces<AdminMfaActivationResponse>();

        admin.MapGet("/dashboard", async (
            AdminQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            var response = await queryService.GetDashboardAsync(cancellationToken);
            return Results.Ok(response);
        })
        .WithName("GetAdminDashboard")
        .Produces<AdminDashboardResponse>();

        admin.MapGet("/payment-providers", async (AdminPaymentProviderService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(cancellationToken)))
            .WithName("ListAdminPaymentProviders")
            .Produces<IReadOnlyList<PaymentProviderResponse>>();

        admin.MapPost("/payment-providers", async (UpsertPaymentProviderRequest request, AdminPaymentProviderService service, CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await service.CreateAsync(request, cancellationToken)); }
            catch (ArgumentException ex) { return Results.BadRequest(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { message = ex.Message }); }
        })
            .WithName("CreateAdminPaymentProvider")
            .Produces<PaymentProviderResponse>();

        admin.MapPut("/payment-providers/{id:guid}", async (Guid id, UpsertPaymentProviderRequest request, AdminPaymentProviderService service, CancellationToken cancellationToken) =>
        {
            try
            {
                var response = await service.UpdateAsync(id, request, cancellationToken);
                return response is null ? Results.NotFound() : Results.Ok(response);
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { message = ex.Message }); }
        })
            .WithName("UpdateAdminPaymentProvider")
            .Produces<PaymentProviderResponse>();
        
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
                GetAuditRequestContext(httpRequest),
                cancellationToken);

            if (result.Status == AdminCmsContentManagementStatus.NotFound)
            {
                return Results.NotFound(new { message = result.Message });
            }

            return Results.Ok(result.Response);
        })
        .WithName("UpdateAdminCmsContentValue");

        admin.MapPost("/cms/media", async (
            HttpRequest httpRequest,
            AdminCmsContentManagementService contentService,
            CmsMediaUploadService uploadService,
            CancellationToken cancellationToken) =>
        {
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
                var response = await contentService.AddMediaAsync(
                    mediaAsset,
                    mediaUrl,
                    GetAdminAuditActor(httpRequest),
                    GetAuditRequestContext(httpRequest),
                    cancellationToken);

                return Results.Ok(response);
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { message = exception.Message });
            }
        })
        .DisableAntiforgery()
        .WithName("UploadStandaloneAdminCmsMedia")
        .Produces<CmsMediaUploadResponse>()
        .Produces(StatusCodes.Status400BadRequest);

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
                    GetAdminAuditActor(httpRequest),
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
            string? service,
            AdminQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            var response = await queryService.ListCompaniesAsync(status, search, service, cancellationToken);
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

        admin.MapGet("/clients", async (
            string? search,
            AdminClientQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await queryService.ListAsync(search, cancellationToken));
        })
        .WithName("ListAdminClients")
        .Produces<AdminClientListResponse>();

        admin.MapGet("/clients/{clientId:guid}", async (
            Guid clientId,
            AdminClientQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            var response = await queryService.GetAsync(clientId, cancellationToken);
            return response is null
                ? Results.NotFound(new { message = "Client introuvable." })
                : Results.Ok(response);
        })
        .WithName("GetAdminClient")
        .Produces<AdminClientDetailResponse>();

        admin.MapGet("/client-attachments/{id:guid}/preview", async (
            Guid id,
            AdminClientQueryService queryService,
            ClientMissionPhotoUploadService uploadService,
            CancellationToken cancellationToken) =>
        {
            var file = await queryService.GetAttachmentFileAsync(id, cancellationToken);
            if (file is null)
            {
                return Results.NotFound(new { message = "Piece client introuvable." });
            }

            var stream = await uploadService.OpenReadAsync(file.Value.StoragePath, cancellationToken);
            return stream is null
                ? Results.NotFound(new { message = "Le fichier client n'existe plus dans le stockage." })
                : Results.Stream(stream, file.Value.ContentType);
        })
        .WithName("PreviewAdminClientAttachment");

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

            var stream = await uploadService.OpenReadAsync(document.StoragePath, cancellationToken);
            return stream is null
                ? Results.NotFound(new { message = "Le fichier prestataire n'existe plus dans le stockage." })
                : Results.Stream(stream, document.ContentType);
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

        admin.MapGet("/missions/provider-test/assignments", async (
            AdminProviderMissionTestService testService,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await testService.GetPendingAsync(cancellationToken));
        })
        .WithName("GetProviderMissionTestAssignments")
        .Produces<AdminProviderMissionTestListResponse>();

        admin.MapPost("/missions/provider-test/assignments/{assignmentId:guid}/validate", async (
            Guid assignmentId,
            AdminProviderMissionTestPositionRequest request,
            AdminProviderMissionTestService testService,
            CancellationToken cancellationToken) =>
        {
            var result = await testService.AcceptAsync(assignmentId, request.EstimatedArrivalMinutes, cancellationToken);
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("ValidateProviderMissionForTest")
        .Produces<AdminProviderMissionTestActionResponse>()
        .Produces<AdminProviderMissionTestActionResponse>(StatusCodes.Status400BadRequest);

        admin.MapPost("/missions/provider-test/assignments/{assignmentId:guid}/position", async (
            Guid assignmentId,
            AdminProviderMissionTestPositionRequest request,
            AdminProviderMissionTestService testService,
            CancellationToken cancellationToken) =>
        {
            var result = await testService.PositionAsync(assignmentId, request.EstimatedArrivalMinutes, cancellationToken);
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("PositionProviderMissionForTest")
        .Produces<AdminProviderMissionTestActionResponse>()
        .Produces<AdminProviderMissionTestActionResponse>(StatusCodes.Status400BadRequest);

        admin.MapPost("/missions/provider-test/assignments/{assignmentId:guid}/start", async (
            Guid assignmentId,
            AdminProviderMissionTestService testService,
            CancellationToken cancellationToken) =>
        {
            var result = await testService.StartAsync(assignmentId, cancellationToken);
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("StartProviderMissionForTest")
        .Produces<AdminProviderMissionTestActionResponse>()
        .Produces<AdminProviderMissionTestActionResponse>(StatusCodes.Status400BadRequest);

        admin.MapPost("/missions/provider-test/assignments/{assignmentId:guid}/complete", async (
            Guid assignmentId,
            AdminProviderMissionTestService testService,
            CancellationToken cancellationToken) =>
        {
            var result = await testService.CompleteAsync(assignmentId, cancellationToken);
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("CompleteProviderMissionForTest")
        .Produces<AdminProviderMissionTestActionResponse>()
        .Produces<AdminProviderMissionTestActionResponse>(StatusCodes.Status400BadRequest);

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

            if (result.Offers.Count > 0)
            {
                // The dispatch round is normally advanced by the client workflow.
                // Admin-triggered dispatches must keep the same eligibility semantics.
                await dispatchService.MarkOffersSentAsync(missionId, cancellationToken);
            }

            return Results.Ok(result.Offers
                .Select(offer => new AdminMissionDispatchOfferResponse(
                    offer.Id,
                    offer.MissionId,
                    offer.CompanyId,
                    offer.Rank,
                    offer.Score,
                    offer.ScoreDetails,
                    offer.Status.ToString(),
                    offer.ExpiresAt))
                .ToList());
        })
        .WithName("CreateAdminMissionDispatchOffers")
        .Produces<IReadOnlyList<AdminMissionDispatchOfferResponse>>()
        .Produces(StatusCodes.Status400BadRequest);

        admin.MapPost("/missions/{missionId:guid}/mark-disputed", async (
            Guid missionId,
            OpenMissionDisputeRequest request,
            HttpRequest httpRequest,
            AdminQueryService queryService,
            AdminMissionDisputeService disputeService,
            CancellationToken cancellationToken) =>
        {
            var result = await disputeService.OpenAsync(
                missionId,
                request.Reason,
                request.Description,
                GetAdminAuditActor(httpRequest),
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
            [FromServices] IAppDbContext db,
            AdminQueryService queryService,
            AdminMissionDisputeService disputeService,
            AdminFinancialAuthorizationService financialAuthorization,
            CancellationToken cancellationToken) =>
        {
            var mission = await db.Missions.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == missionId, cancellationToken);
            if (mission is null)
            {
                return Results.NotFound(new { message = "Mission introuvable." });
            }

            var refundAmount = request.RefundAmount
                ?? (request.RefundPercent is { } refundPercent
                    ? (int)Math.Round(mission.CustomerChargedAmount * Math.Clamp(refundPercent, 0, 100) / 100m, MidpointRounding.AwayFromZero)
                    : 0);
            var trustedPayload = string.Join('|',
                "ResolveMissionDispute",
                mission.Id,
                mission.Status,
                mission.PaymentStatus,
                mission.CustomerChargedAmount,
                request.Resolution.Trim(),
                request.Note.Trim(),
                request.RefundPercent,
                request.RefundAmount,
                request.IncludeCustomerServiceFeeInRefund);
            var authorization = await financialAuthorization.AuthorizeAsync(
                GetCurrentAdminUserId(httpRequest),
                "MissionDisputeResolve",
                missionId,
                trustedPayload,
                request.MfaCode ?? string.Empty,
                refundAmount,
                false,
                cancellationToken);
            if (!authorization.IsAuthorized)
            {
                return authorization.AwaitingSecondApproval
                    ? Results.Accepted(value: ToFinancialActionResponse(authorization))
                    : Results.BadRequest(new { message = authorization.Message });
            }

            var result = await disputeService.ResolveAsync(
                missionId,
                request.Resolution,
                request.Note,
                request.RefundPercent,
                request.RefundAmount,
                request.IncludeCustomerServiceFeeInRefund,
                GetAdminAuditActor(httpRequest),
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

            await financialAuthorization.MarkCompletedAsync(
                "MissionDisputeResolve",
                missionId,
                authorization.PayloadHash!,
                cancellationToken);
            return Results.Ok(ToFinancialActionResponse(authorization, "Litige clôturé et décision financière enregistrée."));
        })
        .WithName("ResolveAdminMissionDispute");

        admin.MapPost("/missions/{missionId:guid}/cancel", async (
            Guid missionId,
            CancelMissionRequest request,
            HttpRequest httpRequest,
            [FromServices] IAppDbContext db,
            AdminQueryService queryService,
            AdminMissionOperationsService missionOperationsService,
            AdminFinancialAuthorizationService financialAuthorization,
            CancellationToken cancellationToken) =>
        {
            var mission = await db.Missions.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == missionId, cancellationToken);
            if (mission is null)
            {
                return Results.NotFound(new { message = "Mission introuvable." });
            }

            var trustedPayload = string.Join('|',
                "CancelMission",
                mission.Id,
                mission.Status,
                mission.PaymentStatus,
                mission.CustomerChargedAmount,
                request.Reason.Trim(),
                request.Comment?.Trim() ?? string.Empty,
                request.CancellationFeeAmount,
                request.RefundPercent,
                request.IncludeCustomerServiceFeeInRefund);
            var authorization = await financialAuthorization.AuthorizeAsync(
                GetCurrentAdminUserId(httpRequest),
                "MissionCancelFinancial",
                missionId,
                trustedPayload,
                request.MfaCode ?? string.Empty,
                mission.CustomerChargedAmount,
                false,
                cancellationToken);
            if (!authorization.IsAuthorized)
            {
                return authorization.AwaitingSecondApproval
                    ? Results.Accepted(value: ToFinancialActionResponse(authorization))
                    : Results.BadRequest(new { message = authorization.Message });
            }

            var result = await missionOperationsService.CancelAsync(
                missionId,
                request.Reason,
                request.Comment,
                request.CancellationFeeAmount,
                request.RefundPercent,
                request.IncludeCustomerServiceFeeInRefund,
                GetAdminAuditActor(httpRequest),
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

            await financialAuthorization.MarkCompletedAsync(
                "MissionCancelFinancial",
                missionId,
                authorization.PayloadHash!,
                cancellationToken);
            return Results.Ok(ToFinancialActionResponse(authorization, "Mission annulée et décision financière enregistrée."));
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
            HttpRequest httpRequest,
            AdminMissionSettingsService settingsService,
            AdminFinancialAuthorizationService financialAuthorization,
            CancellationToken cancellationToken) =>
        {
            var trustedPayload = string.Join('|', "UpdateCommissionRule", ruleId, request.RateBasisPoints, request.FixedAmount, request.Currency.Trim());
            var authorization = await financialAuthorization.AuthorizeAsync(
                GetCurrentAdminUserId(httpRequest),
                "CommissionRuleUpdate",
                ruleId,
                trustedPayload,
                request.MfaCode ?? string.Empty,
                0,
                true,
                cancellationToken);
            if (!authorization.IsAuthorized)
            {
                return authorization.AwaitingSecondApproval
                    ? Results.Accepted(value: ToFinancialActionResponse(authorization))
                    : Results.BadRequest(new { message = authorization.Message });
            }

            var result = await settingsService.UpdateCommissionRuleAsync(ruleId, request, cancellationToken);
            var response = result.Status switch
            {
                AdminMissionSettingsOperationStatus.NotFound => Results.NotFound(new { message = result.Message }),
                AdminMissionSettingsOperationStatus.ValidationFailed => Results.BadRequest(new { message = result.Message }),
                _ => Results.Ok(ToFinancialActionResponse(authorization, "Règle de commission mise à jour."))
            };
            if (result.Status == AdminMissionSettingsOperationStatus.Ok)
            {
                await financialAuthorization.MarkCompletedAsync("CommissionRuleUpdate", ruleId, authorization.PayloadHash!, cancellationToken);
            }

            return response;
        })
        .WithName("UpdateAdminCommissionRule")
        .Produces<AdminFinancialActionResponse>()
        .Produces<AdminFinancialActionResponse>(StatusCodes.Status202Accepted)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        admin.MapPut("/mission-settings/company-commission-tiers/{tierId:guid}", async (
            Guid tierId,
            UpdateAdminCompanyCommissionTierRequest request,
            HttpRequest httpRequest,
            AdminMissionSettingsService settingsService,
            AdminFinancialAuthorizationService financialAuthorization,
            CancellationToken cancellationToken) =>
        {
            var trustedPayload = string.Join('|',
                "UpdateCompanyCommissionTier", tierId, request.Name.Trim(), request.MinimumMissionCount,
                request.RateBasisPoints, request.SortOrder, request.IsActive);
            var authorization = await financialAuthorization.AuthorizeAsync(
                GetCurrentAdminUserId(httpRequest),
                "CompanyCommissionTierUpdate",
                tierId,
                trustedPayload,
                request.MfaCode ?? string.Empty,
                0,
                true,
                cancellationToken);
            if (!authorization.IsAuthorized)
            {
                return authorization.AwaitingSecondApproval
                    ? Results.Accepted(value: ToFinancialActionResponse(authorization))
                    : Results.BadRequest(new { message = authorization.Message });
            }

            var result = await settingsService.UpdateCompanyCommissionTierAsync(tierId, request, cancellationToken);
            var response = result.Status switch
            {
                AdminMissionSettingsOperationStatus.NotFound => Results.NotFound(new { message = result.Message }),
                AdminMissionSettingsOperationStatus.ValidationFailed => Results.BadRequest(new { message = result.Message }),
                _ => Results.Ok(ToFinancialActionResponse(authorization, "Palier de commission mis à jour."))
            };
            if (result.Status == AdminMissionSettingsOperationStatus.Ok)
            {
                await financialAuthorization.MarkCompletedAsync("CompanyCommissionTierUpdate", tierId, authorization.PayloadHash!, cancellationToken);
            }

            return response;
        })
        .WithName("UpdateAdminCompanyCommissionTier")
        .Produces<AdminFinancialActionResponse>()
        .Produces<AdminFinancialActionResponse>(StatusCodes.Status202Accepted)
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
            AdminProviderActionRequest? request,
            HttpRequest httpRequest,
            AdminProviderOperationsService providerOperationsService,
            CancellationToken cancellationToken) =>
        {
            var result = await providerOperationsService.ApproveAsync(
                providerId,
                request?.Note,
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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

        admin.MapPut("/providers/{providerId:guid}/availability", async (
            Guid providerId,
            AdminProviderAvailabilityRequest request,
            HttpRequest httpRequest,
            AdminProviderOperationsService providerOperationsService,
            CancellationToken cancellationToken) =>
        {
            var result = await providerOperationsService.SetAvailabilityAsync(
                providerId,
                request.IsAvailable,
                request.Note,
                GetAdminAuditActor(httpRequest),
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
        .WithName("SetAdminProviderAvailability")
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

        admin.MapGet("/company-payouts", async ([FromServices] IAppDbContext db, CancellationToken cancellationToken) =>
        {
            var payouts = await (from payout in db.CompanyPayoutRequests.AsNoTracking()
                                 join company in db.Companies.AsNoTracking() on payout.CompanyId equals company.Id
                                 join destination in db.CompanyPayoutDestinations.AsNoTracking() on payout.DestinationId equals destination.Id
                                 orderby payout.CreatedAt descending
                                 select new AdminCompanyPayoutResponse(
                                     payout.Id,
                                     company.Id,
                                     company.Name,
                                     payout.Reference,
                                     payout.Method.ToString(),
                                     payout.Status.ToString(),
                                     destination.MaskedIdentifier,
                                     destination.BeneficiaryName,
                                     payout.GrossAmount,
                                     payout.FeeAmount,
                                     payout.NetAmount,
                                     payout.Currency,
                                     payout.CreatedAt,
                                     payout.PaidAt,
                                     payout.ProofReference,
                                     payout.FailureReason))
                .Take(200)
                .ToListAsync(cancellationToken);
            return Results.Ok(payouts);
        })
        .WithName("ListAdminCompanyPayouts")
        .Produces<IReadOnlyList<AdminCompanyPayoutResponse>>();

        admin.MapGet("/company-payout-destinations", async ([FromServices] IAppDbContext db, CancellationToken cancellationToken) =>
        {
            var destinations = await (from destination in db.CompanyPayoutDestinations.AsNoTracking()
                                      join company in db.Companies.AsNoTracking() on destination.CompanyId equals company.Id
                                      orderby destination.IsVerified, destination.CreatedAt descending
                                      select new AdminCompanyPayoutDestinationResponse(
                                          destination.Id,
                                          company.Id,
                                          company.Name,
                                          destination.Method.ToString(),
                                          destination.Label,
                                          destination.BeneficiaryName,
                                          destination.ProviderCode,
                                          destination.MaskedIdentifier,
                                          destination.IsDefault,
                                          destination.IsVerified,
                                          destination.IsActive,
                                          destination.CreatedAt))
                .Take(200)
                .ToListAsync(cancellationToken);
            return Results.Ok(destinations);
        })
        .WithName("ListAdminCompanyPayoutDestinations")
        .Produces<IReadOnlyList<AdminCompanyPayoutDestinationResponse>>();

        admin.MapPost("/company-payout-destinations/{destinationId:guid}/verify", async (
            Guid destinationId,
            VerifyCompanyPayoutDestinationRequest request,
            HttpRequest httpRequest,
            [FromServices] IAppDbContext db,
            CompanyWalletService walletService,
            AdminFinancialAuthorizationService financialAuthorization,
            CancellationToken cancellationToken) =>
        {
            var destination = await db.CompanyPayoutDestinations.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == destinationId, cancellationToken);
            if (destination is null)
            {
                return Results.NotFound();
            }

            var adminUserId = GetCurrentAdminUserId(httpRequest);
            var trustedPayload = string.Join('|',
                "VerifyDestination",
                destination.Id,
                destination.CompanyId,
                destination.Method,
                destination.ProviderCode,
                destination.MaskedIdentifier,
                request.ExternalContactId?.Trim() ?? string.Empty);
            var authorization = await financialAuthorization.AuthorizeAsync(
                adminUserId,
                "CompanyPayoutDestinationVerify",
                destinationId,
                trustedPayload,
                request.MfaCode ?? string.Empty,
                0,
                false,
                cancellationToken);
            if (!authorization.IsAuthorized)
            {
                return authorization.AwaitingSecondApproval
                    ? Results.Accepted(value: ToFinancialActionResponse(authorization))
                    : Results.BadRequest(new { message = authorization.Message });
            }

            if (!await walletService.VerifyDestinationAsync(destinationId, request.ExternalContactId, cancellationToken))
            {
                return Results.NotFound();
            }

            db.AuditLogEntries.Add(AuditLogFactory.Create(
                GetAdminAuditActor(httpRequest),
                "CompanyPayoutDestinationVerified",
                nameof(CompanyPayoutDestination),
                destinationId,
                "Compte beneficiaire de reversement verifie.",
                GetAuditRequestContext(httpRequest),
                after: new { request.ExternalContactId }));
            await db.SaveChangesAsync(cancellationToken);
            await financialAuthorization.MarkCompletedAsync(
                "CompanyPayoutDestinationVerify",
                destinationId,
                authorization.PayloadHash!,
                cancellationToken);
            return Results.Ok(ToFinancialActionResponse(authorization, "Compte de reversement vérifié."));
        })
        .WithName("VerifyCompanyPayoutDestination");

        admin.MapPost("/company-payouts/{payoutId:guid}/approve", async (
            Guid payoutId,
            AdminFinancialActionRequest request,
            HttpRequest httpRequest,
            [FromServices] IAppDbContext db,
            CompanyWalletService walletService,
            AdminFinancialAuthorizationService financialAuthorization,
            CancellationToken cancellationToken) =>
        {
            var payout = await db.CompanyPayoutRequests.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == payoutId, cancellationToken);
            if (payout is null)
            {
                return Results.NotFound();
            }

            var trustedPayload = string.Join('|',
                "ApprovePayout",
                payout.Id,
                payout.CompanyId,
                payout.Reference,
                payout.Status,
                payout.GrossAmount,
                payout.FeeAmount,
                payout.NetAmount,
                payout.Currency);
            var authorization = await financialAuthorization.AuthorizeAsync(
                GetCurrentAdminUserId(httpRequest),
                "CompanyPayoutApprove",
                payoutId,
                trustedPayload,
                request.MfaCode,
                payout.NetAmount,
                false,
                cancellationToken);
            if (!authorization.IsAuthorized)
            {
                return authorization.AwaitingSecondApproval
                    ? Results.Accepted(value: ToFinancialActionResponse(authorization))
                    : Results.BadRequest(new { message = authorization.Message });
            }

            if (!await walletService.ApprovePayoutAsync(payoutId, cancellationToken))
            {
                return Results.NotFound();
            }

            db.AuditLogEntries.Add(AuditLogFactory.Create(
                GetAdminAuditActor(httpRequest),
                "CompanyPayoutApproved",
                nameof(CompanyPayoutRequest),
                payoutId,
                "Reversement entreprise approuve.",
                GetAuditRequestContext(httpRequest)));
            await db.SaveChangesAsync(cancellationToken);
            await financialAuthorization.MarkCompletedAsync(
                "CompanyPayoutApprove",
                payoutId,
                authorization.PayloadHash!,
                cancellationToken);
            return Results.Ok(ToFinancialActionResponse(authorization, "Reversement approuvé."));
        })
        .WithName("ApproveCompanyPayout");

        admin.MapPost("/company-payouts/{payoutId:guid}/reject", async (
            Guid payoutId,
            ReviewCompanyPayoutRequest request,
            HttpRequest httpRequest,
            [FromServices] IAppDbContext db,
            CompanyWalletService walletService,
            AdminFinancialAuthorizationService financialAuthorization,
            CancellationToken cancellationToken) =>
        {
            var payout = await db.CompanyPayoutRequests.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == payoutId, cancellationToken);
            if (payout is null)
            {
                return Results.NotFound();
            }

            var trustedPayload = string.Join('|',
                "RejectPayout",
                payout.Id,
                payout.CompanyId,
                payout.Reference,
                payout.Status,
                payout.GrossAmount,
                request.Reason?.Trim() ?? string.Empty);
            var authorization = await financialAuthorization.AuthorizeAsync(
                GetCurrentAdminUserId(httpRequest),
                "CompanyPayoutReject",
                payoutId,
                trustedPayload,
                request.MfaCode ?? string.Empty,
                payout.GrossAmount,
                false,
                cancellationToken);
            if (!authorization.IsAuthorized)
            {
                return authorization.AwaitingSecondApproval
                    ? Results.Accepted(value: ToFinancialActionResponse(authorization))
                    : Results.BadRequest(new { message = authorization.Message });
            }

            if (!await walletService.RejectPayoutAsync(payoutId, request.Reason, cancellationToken))
            {
                return Results.NotFound();
            }

            db.AuditLogEntries.Add(AuditLogFactory.Create(
                GetAdminAuditActor(httpRequest),
                "CompanyPayoutRejected",
                nameof(CompanyPayoutRequest),
                payoutId,
                "Reversement entreprise rejete.",
                GetAuditRequestContext(httpRequest),
                after: new { request.Reason }));
            await db.SaveChangesAsync(cancellationToken);
            await financialAuthorization.MarkCompletedAsync(
                "CompanyPayoutReject",
                payoutId,
                authorization.PayloadHash!,
                cancellationToken);
            return Results.Ok(ToFinancialActionResponse(authorization, "Reversement rejeté et réservation libérée."));
        })
        .WithName("RejectCompanyPayout");

        admin.MapPost("/company-payouts/{payoutId:guid}/complete-cash", async (
            Guid payoutId,
            ReviewCompanyPayoutRequest request,
            HttpRequest httpRequest,
            [FromServices] IAppDbContext db,
            CompanyWalletService walletService,
            AdminFinancialAuthorizationService financialAuthorization,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.ProofReference))
            {
                return Results.BadRequest(new { message = "Une reference de preuve ou de recu est obligatoire." });
            }

            var payout = await db.CompanyPayoutRequests.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == payoutId, cancellationToken);
            if (payout is null)
            {
                return Results.NotFound();
            }

            var trustedPayload = string.Join('|',
                "CompleteCashPayout",
                payout.Id,
                payout.CompanyId,
                payout.Reference,
                payout.Status,
                payout.GrossAmount,
                payout.NetAmount,
                payout.Currency,
                request.ProofReference.Trim());
            var authorization = await financialAuthorization.AuthorizeAsync(
                GetCurrentAdminUserId(httpRequest),
                "CompanyCashPayoutComplete",
                payoutId,
                trustedPayload,
                request.MfaCode ?? string.Empty,
                payout.NetAmount,
                true,
                cancellationToken);
            if (!authorization.IsAuthorized)
            {
                return authorization.AwaitingSecondApproval
                    ? Results.Accepted(value: ToFinancialActionResponse(authorization))
                    : Results.BadRequest(new { message = authorization.Message });
            }

            var completed = false;
            await db.ExecuteInTransactionAsync(async transactionCancellationToken =>
            {
                completed = await walletService.CompleteCashPayoutAsync(
                    payoutId,
                    request.ProofReference,
                    transactionCancellationToken);
                if (!completed)
                {
                    return;
                }

                var auditExists = await db.AuditLogEntries.AnyAsync(
                    item => item.Action == "CompanyCashPayoutCompleted"
                        && item.EntityType == nameof(CompanyPayoutRequest)
                        && item.EntityId == payoutId,
                    transactionCancellationToken);
                if (!auditExists)
                {
                    db.AuditLogEntries.Add(AuditLogFactory.Create(
                        GetAdminAuditActor(httpRequest),
                        "CompanyCashPayoutCompleted",
                        nameof(CompanyPayoutRequest),
                        payoutId,
                        "Retrait cash entreprise remis et solde debite.",
                        GetAuditRequestContext(httpRequest),
                        after: new { request.ProofReference }));
                    await db.SaveChangesAsync(transactionCancellationToken);
                }
            }, cancellationToken);

            if (!completed)
            {
                return Results.NotFound();
            }

            await financialAuthorization.MarkCompletedAsync(
                "CompanyCashPayoutComplete",
                payoutId,
                authorization.PayloadHash!,
                cancellationToken);
            return Results.Ok(ToFinancialActionResponse(authorization, "Retrait cash confirmé et solde débité."));
        })
        .WithName("CompleteCashCompanyPayout");
        
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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

        admin.MapPost("/access-control/admins/invitations", async (
            CreateAdminUserRequest request,
            HttpRequest httpRequest,
            AdminAccessControlService accessControlService,
            CancellationToken cancellationToken) =>
        {
            var result = await accessControlService.CreateAdminInvitationAsync(
                request,
                GetAdminAuditActor(httpRequest),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            if (result.Status == AdminAccessControlStatus.ValidationFailed)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            if (result.Status == AdminAccessControlStatus.NotFound)
            {
                return Results.NotFound(new { message = result.Message });
            }

            return Results.Ok(result.Invitation);
        })
        .WithName("CreateAdminInvitation");

        admin.MapGet("/access-control/admins/invitations/{token}", async (
            string token,
            AdminAccessControlService accessControlService,
            CancellationToken cancellationToken) =>
        {
            var invitation = await accessControlService.GetInvitationAsync(token, cancellationToken);
            return invitation is null ? Results.NotFound(new { message = "Lien d'invitation introuvable." }) : Results.Ok(invitation);
        })
        .WithName("GetAdminInvitation");

        admin.MapPost("/access-control/admins/invitations/{token}/password", async (
            string token,
            AcceptAdminInvitationRequest request,
            HttpRequest httpRequest,
            AdminAccessControlService accessControlService,
            CancellationToken cancellationToken) =>
        {
            var result = await accessControlService.AcceptInvitationAsync(
                token,
                request,
                GetAdminAuditActor(httpRequest),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            var error = ToAdminAccessControlError(result);
            if (error is not null)
            {
                return error;
            }

            return Results.Ok(result.Snapshot);
        })
        .WithName("AcceptAdminInvitation")
        .RequireRateLimiting(AuthenticationRateLimitingExtensions.PolicyName);

        admin.MapPut("/access-control/admins/{adminUserId:guid}/profile", async (
            Guid adminUserId,
            UpdateAdminUserProfileRequest request,
            HttpRequest httpRequest,
            AdminAccessControlService accessControlService,
            CancellationToken cancellationToken) =>
        {
            var result = await accessControlService.UpdateAdminUserProfileAsync(
                adminUserId,
                request,
                GetAdminAuditActor(httpRequest),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            var error = ToAdminAccessControlError(result);
            if (error is not null)
            {
                return error;
            }

            return Results.Ok(result.Snapshot);
        })
        .WithName("UpdateAdminUserProfile");

        admin.MapPost("/access-control/admins/{adminUserId:guid}/invitation", async (
            Guid adminUserId,
            HttpRequest httpRequest,
            AdminAccessControlService accessControlService,
            CancellationToken cancellationToken) =>
        {
            var result = await accessControlService.RegenerateAdminInvitationAsync(
                adminUserId,
                GetAdminAuditActor(httpRequest),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            if (result.Status == AdminAccessControlStatus.ValidationFailed)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            if (result.Status == AdminAccessControlStatus.NotFound)
            {
                return Results.NotFound(new { message = result.Message });
            }

            return Results.Ok(result.Invitation);
        })
        .WithName("RegenerateAdminInvitation");

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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                    GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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

        admin.MapPost("/company-service-proposals/{id:guid}/reject", async (
            Guid id,
            RejectCompanyServiceProposalRequest request,
            HttpRequest httpRequest,
            AdminCompanyServiceProposalService serviceProposalService,
            CancellationToken cancellationToken) =>
        {
            var result = await serviceProposalService.RejectAsync(
                id,
                request,
                GetAdminAuditActor(httpRequest),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            if (!result.IsSuccess)
            {
                return ToCompanyServiceProposalActionError(result);
            }

            return Results.Ok(await serviceProposalService.ListAsync(cancellationToken));
        })
        .WithName("RejectCompanyServiceProposal")
        .Produces<CompanyServiceProposalListResponse>();

        admin.MapGet("/services", async (
            AdminServiceCatalogManagementService catalogService,
            CancellationToken cancellationToken) =>
                Results.Ok(await catalogService.ListServicesAsync(cancellationToken)))
        .WithName("ListAdminServices")
        .Produces<IReadOnlyList<ServiceSummaryResponse>>();

        admin.MapPost("/services", async (
            UpsertServiceRequest request,
            HttpRequest httpRequest,
            AdminServiceCatalogManagementService catalogService,
            CancellationToken cancellationToken) =>
        {
            var result = await catalogService.CreateServiceAsync(
                request,
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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

        admin.MapDelete("/services/{serviceId:guid}", async (
            Guid serviceId,
            HttpRequest httpRequest,
            AdminServiceCatalogManagementService catalogService,
            CancellationToken cancellationToken) =>
        {
            var result = await catalogService.DeleteServiceAsync(
                serviceId,
                GetAdminAuditActor(httpRequest),
                GetAuditRequestContext(httpRequest),
                cancellationToken);
            var error = ToAdminServiceCatalogOperationError(result);
            return error ?? Results.Ok(result.Response);
        })
        .WithName("DeleteAdminService");

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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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

        admin.MapPost("/service-prestations/{prestationId:guid}/options", async (
            Guid prestationId,
            UpsertServiceOptionRequest request,
            HttpRequest httpRequest,
            AdminServiceCatalogManagementService catalogService,
            CancellationToken cancellationToken) =>
        {
            var result = await catalogService.CreateOptionAsync(prestationId, request,
                GetAdminAuditActor(httpRequest), GetAuditRequestContext(httpRequest), cancellationToken);
            return ToAdminServiceCatalogOperationError(result) ?? Results.Ok(result.Response);
        }).WithName("CreateAdminServiceOption");

        admin.MapPut("/service-options/{id:guid}", async (
            Guid id,
            UpsertServiceOptionRequest request,
            HttpRequest httpRequest,
            AdminServiceCatalogManagementService catalogService,
            CancellationToken cancellationToken) =>
        {
            var result = await catalogService.UpdateOptionAsync(id, request,
                GetAdminAuditActor(httpRequest), GetAuditRequestContext(httpRequest), cancellationToken);
            return ToAdminServiceCatalogOperationError(result) ?? Results.Ok(result.Response);
        }).WithName("UpdateAdminServiceOption");

        admin.MapPost("/service-options/{id:guid}/{state}", async (
            Guid id,
            string state,
            HttpRequest httpRequest,
            AdminServiceCatalogManagementService catalogService,
            CancellationToken cancellationToken) =>
        {
            if (state is not ("activate" or "deactivate")) return Results.BadRequest(new { message = "Etat invalide." });
            var result = await catalogService.SetOptionActiveAsync(id, state == "activate",
                GetAdminAuditActor(httpRequest), GetAuditRequestContext(httpRequest), cancellationToken);
            return ToAdminServiceCatalogOperationError(result) ?? Results.Ok(result.Response);
        }).WithName("SetAdminServiceOptionState");
        
        admin.MapPost("/company-application-documents/{id:guid}/approve", async (
            Guid id,
            HttpRequest httpRequest,
            AdminCompanyApplicationDocumentReviewService documentReviewService,
            CancellationToken cancellationToken) =>
        {
            var result = await documentReviewService.ApproveAsync(
                id,
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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
                GetAdminAuditActor(httpRequest),
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

            Stream? stream;
            try
            {
                stream = await uploadService.OpenReadAsync(document.StoragePath, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                return Results.BadRequest(new { message = "Chemin de document invalide." });
            }

            return stream is null
                ? Results.NotFound(new { message = "Le fichier n'existe plus dans le stockage." })
                : Results.Stream(stream, document.ContentType);
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
        
            Stream? stream;
            try
            {
                stream = await uploadService.OpenReadAsync(document.StoragePath, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                return Results.BadRequest(new { message = "Chemin de document invalide." });
            }
        
            return stream is null
                ? Results.NotFound(new { message = "Le fichier n'existe plus dans le stockage." })
                : Results.Stream(stream, document.ContentType, document.OriginalFileName);
        })
        .WithName("DownloadCompanyApplicationDocument");
        admin.MapGet("/quality", async (AdminQualityManagementService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetDashboardAsync(cancellationToken)))
            .WithName("GetAdminQualityDashboard")
            .Produces<AdminQualityDashboardResponse>();

        admin.MapGet("/quality/templates", async (AdminQualityManagementService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListTemplatesAsync(cancellationToken)))
            .WithName("ListAdminQualityTemplates");

        admin.MapPost("/quality/templates", async (CreateAdminQualityChecklistTemplateRequest request, AdminQualityManagementService service, CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await service.CreateTemplateAsync(request, cancellationToken)); }
            catch (ArgumentException ex) { return Results.BadRequest(new { message = ex.Message }); }
        }).WithName("CreateAdminQualityTemplate");

        admin.MapPut("/quality/templates/{id:guid}", async (Guid id, UpdateAdminQualityChecklistTemplateRequest request, AdminQualityManagementService service, CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await service.UpdateTemplateAsync(id, request, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { message = ex.Message }); }
        }).WithName("UpdateAdminQualityTemplate");

        admin.MapPost("/quality/templates/{id:guid}/items", async (Guid id, CreateAdminQualityChecklistItemRequest request, AdminQualityManagementService service, CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await service.AddItemAsync(id, request, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { message = ex.Message }); }
        }).WithName("CreateAdminQualityTemplateItem");

        admin.MapPut("/quality/items/{id:guid}", async (Guid id, UpdateAdminQualityChecklistItemRequest request, AdminQualityManagementService service, CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await service.UpdateItemAsync(id, request, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { message = ex.Message }); }
        }).WithName("UpdateAdminQualityTemplateItem");

        admin.MapGet("/quality/qualifications", async (string? status, AdminQualityManagementService service, CancellationToken cancellationToken) =>
        {
            ProviderQualificationStatus? parsed = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<ProviderQualificationStatus>(status, true, out var parsedValue))
                    return Results.BadRequest(new { message = "Statut de qualification invalide." });
                parsed = parsedValue;
            }
            return Results.Ok(await service.ListQualificationsAsync(parsed, cancellationToken));
        }).WithName("ListAdminQualityQualifications");

        admin.MapPut("/quality/qualifications/{id:guid}", async (Guid id, ReviewAdminProviderQualificationRequest request, HttpRequest httpRequest, AdminQualityManagementService service, CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await service.ReviewQualificationAsync(id, request, GetCurrentAdminUserId(httpRequest), cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { message = ex.Message }); }
        }).WithName("ReviewAdminQualityQualification");

        admin.MapGet("/quality/audits", async (string? status, int? take, AdminQualityManagementService service, CancellationToken cancellationToken) =>
        {
            QualityAuditStatus? parsed = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<QualityAuditStatus>(status, true, out var parsedValue))
                    return Results.BadRequest(new { message = "Statut d'audit invalide." });
                parsed = parsedValue;
            }
            return Results.Ok(await service.ListAuditsAsync(parsed, take ?? 100, cancellationToken));
        }).WithName("ListAdminQualityAudits");

        admin.MapPut("/quality/audits/{id:guid}", async (Guid id, ReviewAdminQualityAuditRequest request, HttpRequest httpRequest, AdminQualityManagementService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ReviewAuditAsync(id, request, GetCurrentAdminUserId(httpRequest), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).WithName("ReviewAdminQualityAudit");

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

    static AdminFinancialActionResponse ToFinancialActionResponse(
        AdminFinancialAuthorizationResult authorization,
        string? completedMessage = null)
    {
        return new AdminFinancialActionResponse(
            authorization.IsAuthorized,
            authorization.AwaitingSecondApproval,
            authorization.ApprovalsReceived,
            authorization.ApprovalsRequired,
            completedMessage ?? authorization.Message ?? "Confirmation enregistrée.");
    }

    static async ValueTask<object?> AdminSessionEndpointFilterAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;
        if (ShouldSkipAdminSessionCheck(request.Path))
        {
            return await next(context);
        }

        var token = GetAdminSessionToken(request);
        if (string.IsNullOrWhiteSpace(token))
        {
            return Results.Unauthorized();
        }

        var permission = AdminEndpointPermissionResolver.Resolve(request.Method, request.Path.Value ?? string.Empty);
        var authService = context.HttpContext.RequestServices.GetRequiredService<AdminAuthService>();
        var canAccess = await authService.CanAccessAsync(
            token,
            permission.ModuleKey,
            permission.Action,
            context.HttpContext.RequestAborted);

        if (!canAccess)
        {
            return Results.Forbid();
        }

        var currentUser = await authService.GetCurrentUserAsync(token, context.HttpContext.RequestAborted);
        if (currentUser is not null)
        {
            context.HttpContext.Items[CurrentAdminUserItemKey] = currentUser;
        }

        return await next(context);
    }

    static AuditActor GetAdminAuditActor(HttpRequest request)
    {
        return request.HttpContext.Items.TryGetValue(CurrentAdminUserItemKey, out var value)
            && value is AdminCurrentUserResponse currentUser
            ? new AuditActor(AuditActorType.Admin, currentUser.Id, currentUser.FullName)
            : AuditActor.Admin();
    }

    static Guid GetCurrentAdminUserId(HttpRequest request)
    {
        return request.HttpContext.Items.TryGetValue(CurrentAdminUserItemKey, out var value)
            && value is AdminCurrentUserResponse currentUser
            ? currentUser.Id
            : Guid.Empty;
    }

    static bool ShouldSkipAdminSessionCheck(PathString path)
    {
        if (path.StartsWithSegments("/api/admin/auth", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var pathValue = path.Value ?? string.Empty;
        const string InvitationBasePath = "/api/admin/access-control/admins/invitations/";
        return pathValue.StartsWith(InvitationBasePath, StringComparison.OrdinalIgnoreCase);
    }

    static string GetAdminSessionToken(HttpRequest request)
    {
        return request.Headers.TryGetValue("X-Admin-Session", out var values)
            ? values.FirstOrDefault()?.Trim() ?? string.Empty
            : string.Empty;
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
            service.PriceMaxAmount,
            service.IconUrl,
            service.ImageUrl,
            service.DisplayCategory.ToString());
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
            prestation.PriceMaxAmount,
            prestation.IllustrationUrl);
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
