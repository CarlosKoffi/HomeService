using HomeService.Api.Auditing;
using HomeService.Application.Abstractions;
using HomeService.Application.Auditing;
using HomeService.Application.Clients;
using HomeService.Application.Missions;
using HomeService.Application.Notifications;
using HomeService.Application.ProviderPortal;
using HomeService.Application.Quality;
using HomeService.Application.Security;
using HomeService.Contracts.Missions;
using HomeService.Contracts.Notifications;
using HomeService.Contracts.Clients;
using HomeService.Contracts.ProviderPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Api.Endpoints;

public static class ProviderPortalEndpoints
{
    public static IEndpointRouteBuilder MapProviderPortalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/provider-portal");

        group.MapGet("/invitations/{code}", async (
            string code,
            ProviderPortalAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var invitation = await authService.GetInvitationAsync(code, cancellationToken);
            return invitation is null
                ? Results.NotFound(new { message = "Code de preinscription introuvable." })
                : Results.Ok(invitation);
        })
        .WithName("GetProviderInvitation");

        group.MapPost("/activate", async (
            ProviderInvitationActivationRequest request,
            HttpRequest httpRequest,
            ProviderPortalAuthService authService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await authService.ActivateInvitationAsync(request, cancellationToken);
            if (!result.IsSuccess || result.Response is null || result.Provider is null)
            {
                return Results.BadRequest(new { message = result.ErrorMessage ?? "Activation impossible." });
            }

            AddProviderAudit(
                db,
                httpRequest,
                result.Provider.Id,
                result.Provider.FullName,
                "ProviderPortalActivated",
                nameof(ProviderProfile),
                result.Provider.Id,
                "Compte prestataire active depuis un code entreprise.",
                after: new { result.Provider.Status, result.Provider.CompanyId });
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(result.Response);
        })
        .WithName("ActivateProviderInvitation")
        .RequireRateLimiting(AuthenticationRateLimitingExtensions.PolicyName);

        group.MapPost("/login", async (
            ProviderPortalLoginRequest request,
            HttpRequest httpRequest,
            ProviderPortalAuthService authService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await authService.LoginAsync(request, cancellationToken);
            if (!result.IsSuccess || result.Response is null || result.Provider is null)
            {
                return Results.BadRequest(new { message = result.ErrorMessage ?? "Connexion impossible." });
            }

            AddProviderAudit(
                db,
                httpRequest,
                result.Provider.Id,
                result.Provider.FullName,
                "ProviderPortalLogin",
                nameof(ProviderPortalSession),
                result.Session?.Id,
                "Connexion prestataire.",
                after: new { result.Provider.Status, result.Response.ExpiresAt });
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(result.Response);
        })
        .WithName("LoginProviderPortal")
        .RequireRateLimiting(AuthenticationRateLimitingExtensions.PolicyName);

        group.MapGet("/me", async (
            HttpRequest httpRequest,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null)
            {
                return Results.Unauthorized();
            }

            var provider = session.Provider;
            return Results.Ok(new ProviderPortalMeResponse(
                provider.Id,
                provider.FullName,
                provider.PhoneNumber,
                provider.Company?.Name,
                provider.Status.ToString(),
                provider.Status == ProviderStatus.Approved && provider.CompanyId is not null,
                provider.IsAvailable));
        })
        .WithName("GetProviderPortalMe");

        group.MapPost("/mobile/device-token", async (
            RegisterMobileDeviceTokenRequest request,
            HttpRequest httpRequest,
            IAppDbContext db,
            MobileDeviceTokenService deviceTokenService,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null)
            {
                return Results.Unauthorized();
            }

            var result = await deviceTokenService.RegisterAsync(
                MobileDeviceOwnerType.Provider,
                session.ProviderId,
                request,
                cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Response)
                : Results.BadRequest(new { message = result.Message });
        })
        .WithName("RegisterProviderMobileDeviceToken")
        .Produces<MobileDeviceTokenResponse>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/mobile/home", async (
            HttpRequest httpRequest,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null)
            {
                return Results.Unauthorized();
            }

            var provider = await db.Providers
                .AsNoTracking()
                .Include(provider => provider.Company)
                .Include(provider => provider.Documents)
                .Include(provider => provider.Services)
                    .ThenInclude(providerService => providerService.Service)
                .FirstOrDefaultAsync(provider => provider.Id == session.ProviderId, cancellationToken);

            if (provider is null)
            {
                return Results.Unauthorized();
            }

            var now = DateTimeOffset.UtcNow;
            var assignments = await db.ProviderMissionAssignments
                .AsNoTracking()
                .Include(assignment => assignment.Company)
                .Include(assignment => assignment.Mission)
                    .ThenInclude(mission => mission!.ServicePrestation)
                .Where(assignment =>
                    assignment.ProviderId == provider.Id
                    && assignment.Mission != null
                    && assignment.Mission.Status != MissionStatus.Completed
                    && assignment.Mission.Status != MissionStatus.Cancelled
                    && assignment.Mission.Status != MissionStatus.Disputed
                    && assignment.Mission.Status != MissionStatus.Resolved
                    && assignment.Status != ProviderMissionAssignmentStatus.Refused
                    && assignment.Status != ProviderMissionAssignmentStatus.Completed
                    && assignment.Status != ProviderMissionAssignmentStatus.Expired
                    && assignment.Status != ProviderMissionAssignmentStatus.Cancelled)
                .OrderBy(assignment => assignment.Mission!.ScheduledFor ?? assignment.ExpiresAt)
                .Take(6)
                .ToListAsync(cancellationToken);

            var missionRows = assignments
                .Where(assignment => assignment.Mission is not null)
                .Select(assignment => assignment.Mission!)
                .ToList();
            var serviceIds = missionRows.Select(mission => mission.ServiceId).Distinct().ToList();
            var customerIds = missionRows.Select(mission => mission.CustomerId).Distinct().ToList();
            var servicesById = await db.Services
                .AsNoTracking()
                .Where(service => serviceIds.Contains(service.Id))
                .ToDictionaryAsync(service => service.Id, cancellationToken);
            var customersById = await db.Customers
                .AsNoTracking()
                .Where(customer => customerIds.Contains(customer.Id))
                .ToDictionaryAsync(customer => customer.Id, cancellationToken);

            var liveOffer = assignments
                .Where(assignment => assignment.Status == ProviderMissionAssignmentStatus.Offered && assignment.ExpiresAt > now)
                .OrderBy(assignment => assignment.ExpiresAt)
                .Select(assignment => ToProviderMobileMissionOffer(assignment, provider, now, servicesById, customersById))
                .FirstOrDefault();

            var upcomingMission = assignments
                .Where(assignment => assignment.Status is ProviderMissionAssignmentStatus.Accepted
                    or ProviderMissionAssignmentStatus.Started)
                .OrderBy(assignment => assignment.Mission!.ScheduledFor ?? assignment.ExpiresAt)
                .Select(assignment => ToProviderMobileMissionSummary(assignment, servicesById, customersById))
                .FirstOrDefault();

            var hasActiveMission = assignments.Any(assignment =>
                assignment.Status is ProviderMissionAssignmentStatus.Accepted or ProviderMissionAssignmentStatus.Started
                && assignment.Mission!.Status is MissionStatus.Accepted or MissionStatus.OnTheWay or MissionStatus.Started);
            var effectiveAvailability = provider.IsAvailable && !hasActiveMission;

            return Results.Ok(new ProviderMobileHomeResponse(
                new ProviderMobileStatusResponse(
                    provider.FullName,
                    provider.Company?.Name ?? "En attente d'entreprise",
                    effectiveAvailability,
                    effectiveAvailability ? "Disponible" : "Indisponible",
                    provider.MissionRadiusKm,
                    !hasActiveMission,
                    hasActiveMission
                        ? "Indisponible pendant la mission. Vous redeviendrez disponible lorsqu'elle sera terminee ou annulee."
                        : effectiveAvailability
                            ? "Vous pouvez recevoir de nouvelles missions."
                            : "Activez votre disponibilite pour recevoir des missions."),
                BuildProviderMobileProfileCompletion(provider),
                upcomingMission,
                liveOffer));
        })
        .WithName("GetProviderMobileHome");

        group.MapPut("/mobile/availability", async (
            UpdateProviderMobileAvailabilityRequest request,
            HttpRequest httpRequest,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null)
            {
                return Results.Unauthorized();
            }

            var hasActiveMission = await db.ProviderMissionAssignments
                .AsNoTracking()
                .AnyAsync(assignment =>
                    assignment.ProviderId == session.ProviderId
                    && (assignment.Status == ProviderMissionAssignmentStatus.Accepted
                        || assignment.Status == ProviderMissionAssignmentStatus.Started),
                    cancellationToken);

            if (hasActiveMission)
            {
                return Results.BadRequest(new
                {
                    message = "La disponibilite reste verrouillee pendant une mission acceptee."
                });
            }

            var before = new
            {
                session.Provider.IsAvailable,
                session.Provider.CurrentLatitude,
                session.Provider.CurrentLongitude
            };

            try
            {
                session.Provider.SetAvailability(
                    request.IsAvailable,
                    request.Latitude ?? session.Provider.CurrentLatitude,
                    request.Longitude ?? session.Provider.CurrentLongitude);
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { message = exception.Message });
            }

            AddProviderAudit(
                db,
                httpRequest,
                session.ProviderId,
                session.Provider.FullName,
                "ProviderAvailabilityUpdated",
                nameof(ProviderProfile),
                session.ProviderId,
                request.IsAvailable ? "Prestataire disponible." : "Prestataire indisponible.",
                before,
                new
                {
                    session.Provider.IsAvailable,
                    session.Provider.CurrentLatitude,
                    session.Provider.CurrentLongitude
                });

            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new ProviderMobileAvailabilityResponse(
                session.Provider.IsAvailable,
                session.Provider.IsAvailable ? "Disponible" : "Indisponible",
                true,
                session.Provider.IsAvailable
                    ? "Vous pouvez recevoir de nouvelles missions."
                    : "Vous ne recevrez pas de nouvelle mission tant que vous restez indisponible."));
        })
        .WithName("UpdateProviderMobileAvailability")
        .Produces<ProviderMobileAvailabilityResponse>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/mobile/missions", async (
            DateTimeOffset? from,
            DateTimeOffset? to,
            string? status,
            HttpRequest httpRequest,
            IAppDbContext db,
            MobileNavigationBadgeService badgeService,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null)
            {
                return Results.Unauthorized();
            }

            var query = db.ProviderMissionAssignments
                .AsNoTracking()
                .Include(assignment => assignment.Company)
                .Include(assignment => assignment.Mission)
                    .ThenInclude(mission => mission!.ServicePrestation)
                .Where(assignment => assignment.ProviderId == session.ProviderId);

            if (from is not null)
            {
                query = query.Where(assignment =>
                    assignment.Mission != null
                    && (assignment.Mission.ScheduledFor ?? assignment.ExpiresAt) >= from.Value);
            }

            if (to is not null)
            {
                query = query.Where(assignment =>
                    assignment.Mission != null
                    && (assignment.Mission.ScheduledFor ?? assignment.ExpiresAt) <= to.Value);
            }

            if (Enum.TryParse<ProviderMissionAssignmentStatus>(status, true, out var parsedStatus))
            {
                query = query.Where(assignment => assignment.Status == parsedStatus);
            }

            var assignments = await query
                .OrderByDescending(assignment => assignment.Mission!.ScheduledFor ?? assignment.ExpiresAt)
                .Take(100)
                .ToListAsync(cancellationToken);
            var missions = assignments.Where(item => item.Mission is not null).Select(item => item.Mission!).ToList();
            var serviceIds = missions.Select(item => item.ServiceId).Distinct().ToList();
            var customerIds = missions.Select(item => item.CustomerId).Distinct().ToList();
            var servicesById = await db.Services.AsNoTracking()
                .Where(item => serviceIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);
            var customersById = await db.Customers.AsNoTracking()
                .Where(item => customerIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);
            var unreadMessageCounts = await badgeService.GetUnreadMessageCountsByMissionAsync(
                MobileDeviceOwnerType.Provider,
                session.ProviderId,
                cancellationToken);

            var items = assignments
                .Select(assignment => ToProviderMobileMissionSummary(
                    assignment,
                    servicesById,
                    customersById,
                    unreadMessageCounts.GetValueOrDefault(assignment.MissionId)))
                .Where(item => item is not null)
                .Cast<ProviderMobileMissionSummaryResponse>()
                .ToList();
            return Results.Ok(new ProviderMobileMissionListResponse(DateTimeOffset.UtcNow, items));
        })
        .WithName("GetProviderMobileMissions")
        .Produces<ProviderMobileMissionListResponse>()
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/mobile/notifications", async (
            bool? unreadOnly,
            HttpRequest httpRequest,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null)
            {
                return Results.Unauthorized();
            }

            var query = db.NotificationOutboxMessages
                .AsNoTracking()
                .Where(item => item.OwnerType == MobileDeviceOwnerType.Provider
                    && item.OwnerId == session.ProviderId
                    && (item.Channel == NotificationChannel.MobilePush
                        || item.Channel == NotificationChannel.InApp));
            if (unreadOnly == true)
            {
                query = query.Where(item => item.ReadAt == null);
            }

            var rows = await query
                .OrderByDescending(item => item.CreatedAt)
                .Take(100)
                .ToListAsync(cancellationToken);
            var items = rows
                .GroupBy(item => new { item.Subject, item.Body, item.RelatedEntityType, item.RelatedEntityId, item.MetadataJson })
                .Select(group => group.OrderByDescending(item => item.CreatedAt).First())
                .OrderByDescending(item => item.CreatedAt)
                .Take(50)
                .Select(item => new ProviderMobileNotificationResponse(
                    item.Id,
                    item.Subject,
                    item.Body,
                    item.RelatedEntityType,
                    item.RelatedEntityId,
                    item.MetadataJson,
                    item.CreatedAt,
                    item.ReadAt is not null))
                .ToList();
            var unreadCount = rows
                .Where(item => item.ReadAt == null)
                .Select(item => new { item.Subject, item.Body, item.RelatedEntityType, item.RelatedEntityId, item.MetadataJson })
                .Distinct()
                .Count();
            return Results.Ok(new ProviderMobileNotificationListResponse(unreadCount, items));
        })
        .WithName("GetProviderMobileNotifications")
        .Produces<ProviderMobileNotificationListResponse>()
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/mobile/navigation-badges", async (
            HttpRequest httpRequest,
            IAppDbContext db,
            MobileNavigationBadgeService badgeService,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            return session?.Provider is null
                ? Results.Unauthorized()
                : Results.Ok(await badgeService.GetForProviderAsync(session.ProviderId, cancellationToken));
        })
        .WithName("GetProviderMobileNavigationBadges")
        .Produces<MobileNavigationBadgeResponse>()
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/mobile/notifications/{notificationId:guid}/read", async (
            Guid notificationId,
            HttpRequest httpRequest,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null)
            {
                return Results.Unauthorized();
            }

            var notification = await db.NotificationOutboxMessages.FirstOrDefaultAsync(item =>
                item.Id == notificationId
                && item.OwnerType == MobileDeviceOwnerType.Provider
                && item.OwnerId == session.ProviderId,
                cancellationToken);
            if (notification is null)
            {
                return Results.NotFound(new { message = "Notification introuvable." });
            }

            var relatedNotifications = await db.NotificationOutboxMessages
                .Where(item => item.OwnerType == MobileDeviceOwnerType.Provider
                    && item.OwnerId == session.ProviderId
                    && item.Subject == notification.Subject
                    && item.Body == notification.Body
                    && item.RelatedEntityType == notification.RelatedEntityType
                    && item.RelatedEntityId == notification.RelatedEntityId
                    && item.MetadataJson == notification.MetadataJson
                    && item.ReadAt == null)
                .ToListAsync(cancellationToken);
            var readAt = DateTimeOffset.UtcNow;
            foreach (var relatedNotification in relatedNotifications)
            {
                relatedNotification.MarkRead(readAt);
            }
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        })
        .WithName("MarkProviderMobileNotificationRead")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/mobile/notifications/read-all", async (
            HttpRequest httpRequest,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null)
            {
                return Results.Unauthorized();
            }

            var notifications = await db.NotificationOutboxMessages
                .Where(item => item.OwnerType == MobileDeviceOwnerType.Provider
                    && item.OwnerId == session.ProviderId
                    && item.ReadAt == null
                    && (item.Channel == NotificationChannel.MobilePush || item.Channel == NotificationChannel.InApp))
                .ToListAsync(cancellationToken);
            var readAt = DateTimeOffset.UtcNow;
            foreach (var notification in notifications)
            {
                notification.MarkRead(readAt);
            }

            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { updatedCount = notifications.Count });
        })
        .WithName("MarkAllProviderMobileNotificationsRead")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/mobile/profile", async (
            HttpRequest httpRequest,
            IAppDbContext db,
            ProviderMobileProfileService profileService,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null)
            {
                return Results.Unauthorized();
            }

            var result = await profileService.GetAsync(session.ProviderId, cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Response)
                : Results.NotFound(new { message = result.Message });
        })
        .WithName("GetProviderMobileProfile")
        .Produces<ProviderMobileProfileResponse>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/mobile/addresses/autocomplete", async (
            string query,
            string? sessionToken,
            HttpRequest httpRequest,
            IAppDbContext db,
            IAddressAutocompleteService autocompleteService,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null)
            {
                return Results.Unauthorized();
            }

            return query.Trim().Length < 3
                ? Results.Ok(Array.Empty<ClientAddressSuggestionResponse>())
                : Results.Ok(await autocompleteService.SearchAsync(query, sessionToken, cancellationToken));
        })
        .WithName("AutocompleteProviderMobileAddress")
        .Produces<IReadOnlyList<ClientAddressSuggestionResponse>>()
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/mobile/addresses/places/{placeId}", async (
            string placeId,
            string? sessionToken,
            HttpRequest httpRequest,
            IAppDbContext db,
            IAddressAutocompleteService autocompleteService,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null)
            {
                return Results.Unauthorized();
            }

            var details = await autocompleteService.GetDetailsAsync(placeId, sessionToken, cancellationToken);
            return details is null ? Results.NotFound() : Results.Ok(details);
        })
        .WithName("GetProviderMobilePlaceDetails")
        .Produces<ClientPlaceDetailsResponse>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/mobile/profile", async (
            UpdateProviderMobileProfileRequest request,
            HttpRequest httpRequest,
            IAppDbContext db,
            ProviderMobileProfileService profileService,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null)
            {
                return Results.Unauthorized();
            }

            var before = new
            {
                session.Provider.FirstName,
                session.Provider.LastName,
                session.Provider.Email,
                session.Provider.Address,
                session.Provider.MissionRadiusKm,
                session.Provider.MissionLatitude,
                session.Provider.MissionLongitude
            };
            try
            {
                session.Provider.UpdateMobileProfile(
                    request.FirstName,
                    request.LastName,
                    request.Email,
                    request.Address,
                    request.MissionRadiusKm,
                    request.MissionLatitude,
                    request.MissionLongitude);
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { message = exception.Message });
            }

            AddProviderAudit(
                db,
                httpRequest,
                session.ProviderId,
                session.Provider.FullName,
                "ProviderMobileProfileUpdated",
                nameof(ProviderProfile),
                session.ProviderId,
                "Profil prestataire mis a jour depuis l'application mobile.",
                before,
                request);
            await db.SaveChangesAsync(cancellationToken);

            var result = await profileService.GetAsync(session.ProviderId, cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Response)
                : Results.NotFound(new { message = result.Message });
        })
        .WithName("UpdateProviderMobileProfile")
        .Produces<ProviderMobileProfileResponse>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/mobile/profile/documents", async (
            HttpRequest httpRequest,
            IAppDbContext db,
            CompanyProviderUploadService uploadService,
            ProviderMobileProfileUpdateService profileUpdateService,
            ILogger<CompanyProviderUploadService> logger,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null)
            {
                return Results.Unauthorized();
            }

            if (!httpRequest.HasFormContentType)
            {
                return Results.BadRequest(new { message = "La piece doit etre envoyee au format multipart/form-data." });
            }

            IFormCollection form;
            try
            {
                form = await httpRequest.ReadFormAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is BadHttpRequestException or InvalidDataException)
            {
                return Results.BadRequest(new { message = "Le fichier est trop lourd, incomplet ou illisible." });
            }
            if (!TryParseProviderDocumentType(GetOptionalFormValue(form, "documentType"), out var documentType))
            {
                return Results.BadRequest(new { message = "Type de piece invalide." });
            }

            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is null)
            {
                return Results.BadRequest(new { message = "Aucun fichier recu." });
            }

            StoredCompanyProviderDocument stored;
            try
            {
                stored = await uploadService.SaveMobileDocumentAsync(session.ProviderId, documentType, file, cancellationToken);
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { message = exception.Message });
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogError(exception, "Provider mobile document storage failed for provider {ProviderId}.", session.ProviderId);
                return Results.Problem(
                    title: "Stockage de la piece impossible.",
                    detail: "Le fichier n'a pas pu etre enregistre sur le serveur. Verifiez le volume /app/storage et ses droits d'ecriture.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            var result = await profileUpdateService.AddDocumentAsync(
                session.ProviderId,
                stored.DocumentType,
                stored.OriginalFileName,
                stored.StoragePath,
                stored.ContentType,
                cancellationToken);
            if (!result.IsSuccess || result.Response is null)
            {
                await TryDeleteProviderFileAsync(uploadService, stored.StoragePath, cancellationToken);
                return Results.NotFound(new { message = result.Message });
            }

            db.AuditLogEntries.Add(AuditLogFactory.Create(
                AuditActor.Provider(session.ProviderId, session.Provider.FullName),
                "ProviderMobileDocumentUploaded",
                nameof(ProviderDocument),
                result.Response.Id,
                "Piece prestataire ajoutee depuis l'application mobile.",
                HttpAuditContextFactory.Create(httpRequest),
                before: result.Before,
                after: result.After));
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
            {
                await TryDeleteProviderFileAsync(uploadService, stored.StoragePath, cancellationToken);
                logger.LogError(exception, "Provider mobile document upload failed for provider {ProviderId}.", session.ProviderId);
                return Results.Problem(
                    title: "Upload de la piece impossible.",
                    detail: exception.GetBaseException().Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            return Results.Created(result.Response.PreviewUrl, result.Response);
        })
        .WithName("UploadProviderMobileProfileDocument")
        .Produces<ProviderMobileProfileDocumentResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/mobile/profile/documents/{documentId:guid}/preview", async (
            Guid documentId,
            HttpRequest httpRequest,
            IAppDbContext db,
            CompanyProviderUploadService uploadService,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null)
            {
                return Results.Unauthorized();
            }

            var document = await db.ProviderDocuments
                .AsNoTracking()
                .Where(item => item.Id == documentId && item.ProviderId == session.ProviderId)
                .Select(item => new
                {
                    item.OriginalFileName,
                    item.StoragePath,
                    item.ContentType
                })
                .FirstOrDefaultAsync(cancellationToken);

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
                return Results.NotFound(new { message = "Le chemin du fichier prestataire est invalide." });
            }

            return stream is null
                ? Results.NotFound(new { message = "Le fichier prestataire n'existe plus dans le stockage." })
                : Results.Stream(
                    stream,
                    string.IsNullOrWhiteSpace(document.ContentType) ? "application/octet-stream" : document.ContentType);
        })
        .WithName("PreviewProviderMobileProfileDocument")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/mobile/profile/portfolio", async (
            HttpRequest httpRequest,
            IAppDbContext db,
            CompanyProviderUploadService uploadService,
            ProviderMobileProfileUpdateService profileUpdateService,
            ILogger<CompanyProviderUploadService> logger,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null)
            {
                return Results.Unauthorized();
            }

            if (!httpRequest.HasFormContentType)
            {
                return Results.BadRequest(new { message = "La photo de book doit etre envoyee au format multipart/form-data." });
            }

            IFormCollection form;
            try
            {
                form = await httpRequest.ReadFormAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is BadHttpRequestException or InvalidDataException)
            {
                return Results.BadRequest(new { message = "La photo est trop lourde, incomplete ou illisible." });
            }
            if (!Guid.TryParse(GetOptionalFormValue(form, "serviceId"), out var serviceId))
            {
                return Results.BadRequest(new { message = "Service obligatoire pour rattacher la photo de book." });
            }

            var file = form.Files.GetFile("file") ?? form.Files.GetFile("photo") ?? form.Files.FirstOrDefault();
            if (file is null)
            {
                return Results.BadRequest(new { message = "Aucune photo recue." });
            }

            StoredProviderPortfolioFile stored;
            try
            {
                stored = await uploadService.SavePortfolioImageAsync(session.ProviderId, serviceId, file, cancellationToken);
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { message = exception.Message });
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogError(exception, "Provider mobile portfolio storage failed for provider {ProviderId}.", session.ProviderId);
                return Results.Problem(
                    title: "Stockage de la photo impossible.",
                    detail: "Le fichier n'a pas pu etre enregistre sur le serveur. Verifiez le volume /app/storage et ses droits d'ecriture.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            var result = await profileUpdateService.AddPortfolioItemAsync(
                session.ProviderId,
                serviceId,
                stored.OriginalFileName,
                stored.StoragePath,
                stored.ContentType,
                cancellationToken);
            if (!result.IsSuccess || result.Response is null)
            {
                await TryDeleteProviderFileAsync(uploadService, stored.StoragePath, cancellationToken);
                return Results.NotFound(new { message = result.Message });
            }

            db.AuditLogEntries.Add(AuditLogFactory.Create(
                AuditActor.Provider(session.ProviderId, session.Provider.FullName),
                "ProviderMobilePortfolioUploaded",
                nameof(ProviderServicePortfolioItem),
                result.Response.Id,
                "Photo de book prestataire ajoutee depuis l'application mobile.",
                HttpAuditContextFactory.Create(httpRequest),
                after: result.After));
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
            {
                await TryDeleteProviderFileAsync(uploadService, stored.StoragePath, cancellationToken);
                logger.LogError(exception, "Provider mobile portfolio upload failed for provider {ProviderId}.", session.ProviderId);
                return Results.Problem(
                    title: "Upload de la photo impossible.",
                    detail: exception.GetBaseException().Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            return Results.Created(result.Response.PreviewUrl, result.Response);
        })
        .WithName("UploadProviderMobilePortfolioPhoto")
        .Produces<ProviderMobilePortfolioUploadResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/mobile/profile/portfolio/{itemId:guid}/preview", async (
            Guid itemId,
            HttpRequest httpRequest,
            IAppDbContext db,
            CompanyProviderUploadService uploadService,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null)
            {
                return Results.Unauthorized();
            }

            var item = await db.ProviderServicePortfolioItems
                .AsNoTracking()
                .Where(portfolioItem => portfolioItem.Id == itemId && portfolioItem.ProviderId == session.ProviderId)
                .Select(portfolioItem => new
                {
                    portfolioItem.OriginalFileName,
                    portfolioItem.StoragePath,
                    portfolioItem.ContentType
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (item is null)
            {
                return Results.NotFound();
            }

            Stream? stream;
            try
            {
                stream = await uploadService.OpenReadAsync(item.StoragePath, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(new { message = "Le chemin de la photo de book est invalide." });
            }

            return stream is null
                ? Results.NotFound(new { message = "La photo de book n'existe plus dans le stockage." })
                : Results.Stream(
                    stream,
                    string.IsNullOrWhiteSpace(item.ContentType) ? "application/octet-stream" : item.ContentType);
        })
        .WithName("PreviewProviderMobilePortfolioPhoto")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/mobile/mission-assignments/{assignmentId:guid}", async (
            Guid assignmentId,
            HttpRequest httpRequest,
            IAppDbContext db,
            ProviderMobileMissionDetailService detailService,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null)
            {
                return Results.Unauthorized();
            }

            var result = await detailService.GetAsync(session.ProviderId, assignmentId, cancellationToken);
            if (result.IsSuccess)
            {
                return Results.Ok(result.Response);
            }

            return result.Status switch
            {
                ProviderMobileMissionDetailResultStatus.Forbidden => Results.Forbid(),
                ProviderMobileMissionDetailResultStatus.NotFound => Results.NotFound(new { message = result.Message }),
                _ => Results.BadRequest(new { message = result.Message })
            };
        })
        .WithName("GetProviderMobileMissionDetail")
        .Produces<ProviderMobileMissionDetailResponse>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/mobile/mission-assignments/{assignmentId:guid}/messages", async (
            Guid assignmentId,
            HttpRequest httpRequest,
            IAppDbContext db,
            ProviderMissionChatService chatService,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null)
            {
                return Results.Unauthorized();
            }

            var result = await chatService.ListAsync(session.ProviderId, assignmentId, cancellationToken);
            return result.Status switch
            {
                ProviderMissionChatResultStatus.Success => Results.Ok(result.ChatResponse),
                ProviderMissionChatResultStatus.NotFound => Results.NotFound(new { message = result.Message }),
                _ => Results.BadRequest(new { message = result.Message })
            };
        })
        .WithName("ListProviderMissionMessages")
        .Produces<ProviderMissionChatResponse>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/mobile/mission-assignments/{assignmentId:guid}/messages", async (
            Guid assignmentId,
            SendProviderMissionMessageRequest request,
            HttpRequest httpRequest,
            IAppDbContext db,
            ProviderMissionChatService chatService,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null)
            {
                return Results.Unauthorized();
            }

            var result = await chatService.SendAsync(session.ProviderId, assignmentId, request, cancellationToken);
            return result.Status switch
            {
                ProviderMissionChatResultStatus.Created => Results.Created(
                    $"/api/provider-portal/mobile/mission-assignments/{assignmentId}/messages",
                    result.SendResponse),
                ProviderMissionChatResultStatus.NotFound => Results.NotFound(new { message = result.Message }),
                _ => Results.BadRequest(new { message = result.Message })
            };
        })
        .WithName("SendProviderMissionMessage")
        .Produces<SendProviderMissionMessageResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/mobile/mission-assignments/{assignmentId:guid}/accept", AcceptProviderMissionAsync)
            .WithName("AcceptProviderMobileMission");

        group.MapPost("/mobile/mission-assignments/{assignmentId:guid}/refuse", RefuseProviderMissionAsync)
            .WithName("RefuseProviderMobileMission");

        group.MapPost("/mobile/mission-assignments/{assignmentId:guid}/on-the-way", MarkProviderOnTheWayAsync)
            .WithName("MarkProviderMobileMissionOnTheWay");

        group.MapPost("/mobile/mission-assignments/{assignmentId:guid}/verify-arrival", VerifyProviderMissionArrivalAsync)
            .WithName("VerifyProviderMobileMissionArrival");

        group.MapPost("/mobile/mission-assignments/{assignmentId:guid}/start", StartProviderMissionAsync)
            .WithName("StartProviderMobileMissionWithArrivalVerification");

        group.MapPost("/mobile/mission-assignments/{assignmentId:guid}/complete", CompleteProviderMissionAsync)
            .WithName("CompleteProviderMobileMission");

        group.MapGet("/mobile/mission-assignments/{assignmentId:guid}/quality", async (
            Guid assignmentId,
            HttpRequest httpRequest,
            IAppDbContext db,
            MissionQualityChecklistService qualityService,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null) return Results.Unauthorized();
            var response = await qualityService.GetForProviderAsync(session.ProviderId, assignmentId, cancellationToken);
            return response is null ? Results.NotFound(new { message = "Mission introuvable." }) : Results.Ok(response);
        }).WithName("GetProviderMissionQualityChecklist");

        group.MapPut("/mobile/mission-assignments/{assignmentId:guid}/quality/items/{itemId:guid}", async (
            Guid assignmentId,
            Guid itemId,
            UpdateProviderMissionQualityItemRequest request,
            HttpRequest httpRequest,
            IAppDbContext db,
            MissionQualityChecklistService qualityService,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null) return Results.Unauthorized();
            var result = await qualityService.RespondAsync(session.ProviderId, assignmentId, itemId, request, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Checklist) : result.IsNotFound ? Results.NotFound(new { message = result.Message }) : Results.BadRequest(new { message = result.Message });
        }).WithName("UpdateProviderMissionQualityItem");

        group.MapGet("/mobile/mission-assignments/{assignmentId:guid}/quality/items/{itemId:guid}/photo", async (
            Guid assignmentId,
            Guid itemId,
            HttpRequest httpRequest,
            IAppDbContext db,
            CompanyProviderUploadService uploadService,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null) return Results.Unauthorized();

            var assignment = await db.ProviderMissionAssignments
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == assignmentId && item.ProviderId == session.ProviderId, cancellationToken);
            if (assignment is null) return Results.NotFound(new { message = "Mission introuvable." });

            var evidence = await (from qualityItem in db.MissionQualityItems.AsNoTracking()
                                  join control in db.MissionQualityControls.AsNoTracking()
                                      on qualityItem.ControlId equals control.Id
                                  join attachment in db.MissionAttachments.AsNoTracking()
                                      on qualityItem.EvidenceAttachmentId equals attachment.Id
                                  where qualityItem.Id == itemId
                                      && control.MissionId == assignment.MissionId
                                      && !attachment.IsDeleted
                                  select new
                                  {
                                      attachment.StoragePath,
                                      attachment.ContentType
                                  }).FirstOrDefaultAsync(cancellationToken);
            if (evidence is null) return Results.NotFound(new { message = "Aucune photo n'est associee a ce controle." });

            Stream? stream;
            try
            {
                stream = await uploadService.OpenReadAsync(evidence.StoragePath, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(new { message = "Le chemin de la photo est invalide." });
            }

            return stream is null
                ? Results.NotFound(new { message = "La photo n'existe plus dans le stockage." })
                : Results.Stream(
                    stream,
                    string.IsNullOrWhiteSpace(evidence.ContentType) ? "application/octet-stream" : evidence.ContentType);
        })
        .WithName("PreviewProviderMissionQualityPhoto")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/mobile/mission-assignments/{assignmentId:guid}/quality/items/{itemId:guid}/photo", async (
            Guid assignmentId,
            Guid itemId,
            HttpRequest httpRequest,
            IAppDbContext db,
            CompanyProviderUploadService uploadService,
            MissionQualityChecklistService qualityService,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null) return Results.Unauthorized();
            if (!httpRequest.HasFormContentType) return Results.BadRequest(new { message = "Photo attendue au format multipart/form-data." });
            var assignment = await db.ProviderMissionAssignments.Include(item => item.Mission)
                .FirstOrDefaultAsync(item => item.Id == assignmentId && item.ProviderId == session.ProviderId, cancellationToken);
            if (assignment?.Mission is null) return Results.NotFound(new { message = "Mission introuvable." });
            if (assignment.Mission.Status != MissionStatus.Started)
                return Results.BadRequest(new { message = "Les preuves de la checklist sont disponibles uniquement pendant la mission." });
            var form = await httpRequest.ReadFormAsync(cancellationToken);
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is null) return Results.BadRequest(new { message = "Aucune photo recue." });
            try
            {
                var stored = await uploadService.SaveMissionQualityImageAsync(session.ProviderId, assignment.MissionId, file, cancellationToken);
                var checklist = await qualityService.GetForProviderAsync(session.ProviderId, assignmentId, cancellationToken);
                var stage = checklist?.Stages.SelectMany(item => item.Items).FirstOrDefault(item => item.ItemId == itemId);
                var attachment = new MissionAttachment(assignment.MissionId,
                    stage?.Code == "final-photo" ? MissionAttachmentType.ProviderCompletionPhoto : MissionAttachmentType.ProviderStartPhoto,
                    stored.OriginalFileName, stored.StoragePath, stored.ContentType, file.Length, "Preuve checklist qualite");
                db.MissionAttachments.Add(attachment);
                await db.SaveChangesAsync(cancellationToken);
                var result = await qualityService.RespondAsync(session.ProviderId, assignmentId, itemId,
                    new UpdateProviderMissionQualityItemRequest(true, null, null, attachment.Id), cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(new ProviderMissionQualityPhotoUploadResponse(attachment.Id, stored.OriginalFileName, stored.ContentType, file.Length))
                    : Results.BadRequest(new { message = result.Message });
            }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
        }).WithName("UploadProviderMissionQualityPhoto").DisableAntiforgery();

        group.MapPost("/mobile/mission-assignments/{assignmentId:guid}/cancel", CancelProviderMissionAsync)
            .WithName("CancelProviderMobileMission");

        group.MapPost("/mobile/mission-assignments/{assignmentId:guid}/additional-quotes/request", async (
            Guid assignmentId,
            RequestMissionAdditionalQuoteRequest request,
            HttpRequest httpRequest,
            IAppDbContext db,
            MissionAdditionalQuoteWorkflowService additionalQuoteService,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null)
            {
                return Results.Unauthorized();
            }

            var assignment = await db.ProviderMissionAssignments
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.Id == assignmentId
                    && item.ProviderId == session.ProviderId,
                    cancellationToken);
            if (assignment is null)
            {
                return Results.NotFound(new { message = "Mission introuvable pour ce prestataire." });
            }

            var result = await additionalQuoteService.RequestFromProviderAsync(
                session.ProviderId,
                assignment.MissionId,
                request,
                cancellationToken);
            if (result.IsSuccess)
            {
                return Results.Ok(result.Response);
            }

            return result.Status == MissionAdditionalQuoteWorkflowStatus.NotFound
                ? Results.NotFound(new { message = result.Message })
                : Results.BadRequest(new { message = result.Message });
        })
        .WithName("RequestProviderMobileMissionAdditionalQuote")
        .Produces<MissionAdditionalQuoteResponse>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/mission-assignments/{assignmentId:guid}/accept", async (
            Guid assignmentId,
            ProviderAcceptMissionRequest request,
            HttpRequest httpRequest,
            IAppDbContext db,
            ProviderMissionWorkflowService workflow,
            ProviderMissionNotificationService notifications,
            ILogger<ProviderMissionNotificationService> logger,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null)
            {
                return Results.Unauthorized();
            }

            var assignment = await db.ProviderMissionAssignments
                .Include(assignment => assignment.Mission)
                .FirstOrDefaultAsync(assignment =>
                    assignment.Id == assignmentId
                    && assignment.ProviderId == session.ProviderId,
                    cancellationToken);

            if (assignment?.Mission is null)
            {
                return Results.NotFound(new { message = "Mission introuvable pour ce prestataire." });
            }

            var previousStatus = assignment.Status;
            var customerPaymentWindow = await MissionWorkflowSettingsResolver.ResolveMinutesAsync(
                db,
                MissionWorkflowSettingsResolver.CustomerQuoteValidityMinutes,
                30,
                cancellationToken);
            var result = workflow.AcceptMission(
                session.Provider,
                assignment,
                request,
                DateTimeOffset.UtcNow.Add(customerPaymentWindow));
            if (result.Status != ProviderMissionOperationStatus.Ok)
            {
                return ToProviderMissionHttpResult(result);
            }

            AddProviderAudit(
                db,
                httpRequest,
                session.ProviderId,
                $"{session.Provider.FirstName} {session.Provider.LastName}",
                "ProviderMissionAccepted",
                nameof(ProviderMissionAssignment),
                assignment.Id,
                "Mission acceptee par le prestataire. Les contacts restent masques jusqu'a validation client.",
                after: new
                {
                    assignment.MissionId,
                    AssignmentStatus = assignment.Status,
                    MissionStatus = assignment.Mission.Status,
                    assignment.AcceptedLatitude,
                    assignment.AcceptedLongitude,
                    assignment.AcceptedAccuracyMeters,
                    assignment.Mission.ProviderAcceptedAt,
                    assignment.Mission.ContactDetailsReleasedAt
                });

            // La transition métier doit être durable avant les notifications : un incident
            // Firebase ou un modèle de notification absent ne doit jamais annuler l'acceptation.
            await db.SaveChangesAsync(cancellationToken);

            if (previousStatus != ProviderMissionAssignmentStatus.Accepted)
            {
                try
                {
                    await notifications.NotifyAcceptedAsync(
                        assignment.Mission,
                        session.Provider,
                        assignment,
                        cancellationToken);
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "Mission {MissionId} accepted by provider {ProviderId}, but accepted notifications failed.",
                        assignment.MissionId,
                        session.ProviderId);
                }
            }
            return ToProviderMissionHttpResult(result);
        })
        .WithName("AcceptProviderMission");

        group.MapPost("/mobile/mission-assignments/{assignmentId:guid}/location", async (
            Guid assignmentId,
            ProviderLocationVerificationRequest request,
            HttpRequest httpRequest,
            IAppDbContext db,
            ProviderMissionWorkflowService workflow,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null)
            {
                return Results.Unauthorized();
            }

            var assignment = await db.ProviderMissionAssignments
                .Include(item => item.Mission)
                .FirstOrDefaultAsync(item =>
                    item.Id == assignmentId
                    && item.ProviderId == session.ProviderId,
                    cancellationToken);

            if (assignment?.Mission is null)
            {
                return Results.NotFound(new { message = "Mission introuvable pour ce prestataire." });
            }

            var result = workflow.UpdatePosition(session.Provider, assignment, request);
            if (result.Status != ProviderMissionOperationStatus.Ok)
            {
                return ToProviderMissionHttpResult(result);
            }

            await db.SaveChangesAsync(cancellationToken);
            return ToProviderMissionHttpResult(result);
        })
        .WithName("UpdateProviderMissionLocation");

        group.MapPost("/mission-assignments/{assignmentId:guid}/refuse", async (
            Guid assignmentId,
            ProviderRefuseMissionRequest request,
            HttpRequest httpRequest,
            IAppDbContext db,
            ProviderMissionWorkflowService workflow,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null)
            {
                return Results.Unauthorized();
            }

            var assignment = await db.ProviderMissionAssignments
                .Include(assignment => assignment.Mission)
                .FirstOrDefaultAsync(assignment =>
                    assignment.Id == assignmentId
                    && assignment.ProviderId == session.ProviderId,
                    cancellationToken);

            if (assignment?.Mission is null)
            {
                return Results.NotFound(new { message = "Mission introuvable pour ce prestataire." });
            }

            var result = workflow.RefuseMission(session.Provider, assignment, request);
            if (result.Status != ProviderMissionOperationStatus.Ok)
            {
                return ToProviderMissionHttpResult(result);
            }

            var reasonLabel = assignment.RefusalReason?.ToString() ?? "Non renseignee";
            AddProviderAudit(
                db,
                httpRequest,
                session.ProviderId,
                $"{session.Provider.FirstName} {session.Provider.LastName}",
                "ProviderMissionRefused",
                nameof(ProviderMissionAssignment),
                assignment.Id,
                "Mission refusee par le prestataire.",
                after: new
                {
                    assignment.MissionId,
                    AssignmentStatus = assignment.Status,
                    assignment.RefusalReason,
                    assignment.RefusalComment,
                    assignment.RespondedAt
                });

            db.CompanyPortalActivities.Add(new CompanyPortalActivity(
                assignment.CompanyId,
                "mission",
                "Mission refusee",
                $"{session.Provider.FullName} a refuse la mission {assignment.Mission.MissionNumber}. Raison: {reasonLabel}.",
                "orange",
                nameof(ProviderMissionAssignment),
                assignment.Id));

            await db.SaveChangesAsync(cancellationToken);
            return ToProviderMissionHttpResult(result);
        })
        .WithName("RefuseProviderMission");

        group.MapPost("/mission-assignments/{assignmentId:guid}/cancel", CancelProviderMissionAsync)
        .WithName("CancelProviderMission");

        group.MapPost("/mission-assignments/{assignmentId:guid}/verify-arrival", async (
            Guid assignmentId,
            ProviderLocationVerificationRequest request,
            HttpRequest httpRequest,
            IAppDbContext db,
            ProviderMissionWorkflowService workflow,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null)
            {
                return Results.Unauthorized();
            }

            var assignment = await db.ProviderMissionAssignments
                .Include(assignment => assignment.Mission)
                .FirstOrDefaultAsync(assignment =>
                    assignment.Id == assignmentId
                    && assignment.ProviderId == session.ProviderId,
                    cancellationToken);

            if (assignment?.Mission is null)
            {
                return Results.NotFound(new { message = "Mission introuvable pour ce prestataire." });
            }

            var result = workflow.VerifyArrival(session.Provider, assignment, request);
            if (result.Status != ProviderMissionOperationStatus.Ok)
            {
                return ToProviderMissionHttpResult(result);
            }

            AddProviderAudit(
                db,
                httpRequest,
                session.ProviderId,
                $"{session.Provider.FirstName} {session.Provider.LastName}",
                "ProviderArrivalVerified",
                nameof(ProviderMissionAssignment),
                assignment.Id,
                "Arrivee prestataire verifiee pour une mission.",
                after: new
                {
                    assignment.MissionId,
                    assignment.ArrivalVerificationStatus,
                    assignment.ArrivalVerifiedAt,
                    assignment.ArrivalDistanceMeters
                });
            await db.SaveChangesAsync(cancellationToken);
            return ToProviderMissionHttpResult(result);
        })
        .WithName("VerifyProviderMissionArrival");

        group.MapPost("/mission-assignments/{assignmentId:guid}/start", async (
            Guid assignmentId,
            ProviderLocationVerificationRequest request,
            HttpRequest httpRequest,
            IAppDbContext db,
            ProviderMissionWorkflowService workflow,
            MissionPaymentMilestoneService milestoneService,
            ProviderMissionNotificationService notifications,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null)
            {
                return Results.Unauthorized();
            }

            var assignment = await db.ProviderMissionAssignments
                .Include(assignment => assignment.Mission)
                .FirstOrDefaultAsync(assignment =>
                    assignment.Id == assignmentId
                    && assignment.ProviderId == session.ProviderId,
                    cancellationToken);

            if (assignment?.Mission is null)
            {
                return Results.NotFound(new { message = "Mission introuvable pour ce prestataire." });
            }

            var previousStatus = assignment.Status;
            var result = workflow.StartMission(session.Provider, assignment, request);
            if (result.Status != ProviderMissionOperationStatus.Ok)
            {
                if (result.Response is not null)
                {
                    AddProviderAudit(
                        db,
                        httpRequest,
                        session.ProviderId,
                        $"{session.Provider.FirstName} {session.Provider.LastName}",
                        "ProviderMissionStartRejected",
                        nameof(ProviderMissionAssignment),
                        assignment.Id,
                        result.Message ?? "Demarrage mission refuse.",
                        after: new
                        {
                            assignment.MissionId,
                            result.Response.Status,
                            result.Response.DistanceMeters
                        });
                    await db.SaveChangesAsync(cancellationToken);
                }

                return ToProviderMissionHttpResult(result);
            }

            AddProviderAudit(
                db,
                httpRequest,
                session.ProviderId,
                $"{session.Provider.FirstName} {session.Provider.LastName}",
                "ProviderMissionStarted",
                nameof(ProviderMissionAssignment),
                assignment.Id,
                "Mission demarree par le prestataire.",
                after: new
                {
                    assignment.MissionId,
                    AssignmentStatus = assignment.Status,
                    MissionStatus = assignment.Mission.Status,
                    assignment.StartedAt
                });
            await milestoneService.EnsureMissionStartedMilestoneAsync(assignment.Mission, cancellationToken);
            if (previousStatus != ProviderMissionAssignmentStatus.Started)
            {
                await notifications.NotifyStartedAsync(
                    assignment.Mission,
                    session.Provider,
                    assignment,
                    cancellationToken);
            }
            db.CompanyPortalActivities.Add(new CompanyPortalActivity(
                assignment.CompanyId,
                "mission",
                "Mission demarree",
                $"{session.Provider.FullName} a demarre la mission {assignment.Mission.MissionNumber}. Les fonds client restent securises.",
                "blue",
                nameof(Mission),
                assignment.MissionId));
            await db.SaveChangesAsync(cancellationToken);
            return ToProviderMissionHttpResult(result);
        })
        .WithName("StartProviderMissionWithArrivalVerification");

        group.MapPost("/mission-assignments/{assignmentId:guid}/complete", async (
            Guid assignmentId,
            ProviderCompleteMissionRequest request,
            HttpRequest httpRequest,
            IAppDbContext db,
            ProviderMissionWorkflowService workflow,
            MissionPaymentMilestoneService milestoneService,
            CancellationToken cancellationToken) =>
        {
            var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
            if (session?.Provider is null)
            {
                return Results.Unauthorized();
            }

            var assignment = await db.ProviderMissionAssignments
                .Include(assignment => assignment.Mission)
                .FirstOrDefaultAsync(assignment =>
                    assignment.Id == assignmentId
                    && assignment.ProviderId == session.ProviderId,
                    cancellationToken);

            if (assignment?.Mission is null)
            {
                return Results.NotFound(new { message = "Mission introuvable pour ce prestataire." });
            }

            var customerValidationWindow = await MissionWorkflowSettingsResolver.ResolveMinutesAsync(
                db,
                MissionWorkflowSettingsResolver.CustomerCompletionValidationMinutes,
                120,
                cancellationToken);
            var hasBlockingAdditionalQuote = await db.MissionAdditionalQuotes
                .AsNoTracking()
                .AnyAsync(item => item.MissionId == assignment.MissionId
                    && (item.Status == MissionAdditionalQuoteStatus.Requested
                        || item.Status == MissionAdditionalQuoteStatus.Submitted), cancellationToken);
            var result = workflow.CompleteMission(
                session.Provider,
                assignment,
                request,
                DateTimeOffset.UtcNow.Add(customerValidationWindow),
                hasBlockingAdditionalQuote);
            if (result.Status != ProviderMissionOperationStatus.Ok)
            {
                return ToProviderMissionHttpResult(result);
            }

            AddProviderAudit(
                db,
                httpRequest,
                session.ProviderId,
                $"{session.Provider.FirstName} {session.Provider.LastName}",
                "ProviderMissionCompleted",
                nameof(ProviderMissionAssignment),
                assignment.Id,
                "Mission terminee par le prestataire.",
                after: new
                {
                    assignment.MissionId,
                    AssignmentStatus = assignment.Status,
                    MissionStatus = assignment.Mission.Status,
                    request.ActualDurationMinutes,
                    assignment.CompletedAt
                });

            await milestoneService.EnsureMissionCompletedMilestoneAsync(assignment.Mission, cancellationToken);
            db.CompanyPortalActivities.Add(new CompanyPortalActivity(
                assignment.CompanyId,
                "mission",
                "Mission terminee",
                $"{session.Provider.FullName} a termine la mission {assignment.Mission.MissionNumber}. Paiement entreprise a liberer apres validation client.",
                "green",
                nameof(Mission),
                assignment.MissionId));

            await db.SaveChangesAsync(cancellationToken);
            return ToProviderMissionHttpResult(result);
        })
        .WithName("CompleteProviderMission");

        return app;
    }

    private static async Task<IResult> AcceptProviderMissionAsync(
        Guid assignmentId,
        ProviderAcceptMissionRequest request,
        HttpRequest httpRequest,
        IAppDbContext db,
        ProviderMissionWorkflowService workflow,
        ProviderMissionNotificationService notifications,
        ILogger<ProviderMissionNotificationService> logger,
        CancellationToken cancellationToken)
    {
        var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
        if (session?.Provider is null)
        {
            return Results.Unauthorized();
        }

        var assignment = await db.ProviderMissionAssignments
            .Include(assignment => assignment.Mission)
            .FirstOrDefaultAsync(assignment =>
                assignment.Id == assignmentId
                && assignment.ProviderId == session.ProviderId,
                cancellationToken);

        if (assignment?.Mission is null)
        {
            return Results.NotFound(new { message = "Mission introuvable pour ce prestataire." });
        }

        var previousStatus = assignment.Status;
        var customerPaymentWindow = await MissionWorkflowSettingsResolver.ResolveMinutesAsync(
            db,
            MissionWorkflowSettingsResolver.CustomerQuoteValidityMinutes,
            30,
            cancellationToken);
        var result = workflow.AcceptMission(
            session.Provider,
            assignment,
            request,
            DateTimeOffset.UtcNow.Add(customerPaymentWindow));
        if (result.Status != ProviderMissionOperationStatus.Ok)
        {
            return ToProviderMissionHttpResult(result);
        }

        AddProviderAudit(
            db,
            httpRequest,
            session.ProviderId,
            session.Provider.FullName,
            "ProviderMissionAccepted",
            nameof(ProviderMissionAssignment),
            assignment.Id,
            "Mission acceptee par le prestataire depuis l'application mobile.",
            after: new
            {
                assignment.MissionId,
                AssignmentStatus = assignment.Status,
                MissionStatus = assignment.Mission.Status,
                assignment.AcceptedLatitude,
                assignment.AcceptedLongitude,
                assignment.AcceptedAccuracyMeters,
                assignment.Mission.ProviderAcceptedAt,
                assignment.Mission.ContactDetailsReleasedAt
            });

        // Persist the acceptance independently from secondary notification delivery.
        await db.SaveChangesAsync(cancellationToken);

        if (previousStatus != ProviderMissionAssignmentStatus.Accepted)
        {
            try
            {
                await notifications.NotifyAcceptedAsync(assignment.Mission, session.Provider, assignment, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Mission {MissionId} accepted by provider {ProviderId}, but accepted notifications failed.",
                    assignment.MissionId,
                    session.ProviderId);
            }
        }

        return ToProviderMissionHttpResult(result);
    }

    private static async Task<IResult> RefuseProviderMissionAsync(
        Guid assignmentId,
        ProviderRefuseMissionRequest request,
        HttpRequest httpRequest,
        IAppDbContext db,
        ProviderMissionWorkflowService workflow,
        ProviderMissionNotificationService notifications,
        CancellationToken cancellationToken)
    {
        var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
        if (session?.Provider is null)
        {
            return Results.Unauthorized();
        }

        var assignment = await db.ProviderMissionAssignments
            .Include(assignment => assignment.Mission)
            .FirstOrDefaultAsync(assignment =>
                assignment.Id == assignmentId
                && assignment.ProviderId == session.ProviderId,
                cancellationToken);

        if (assignment?.Mission is null)
        {
            return Results.NotFound(new { message = "Mission introuvable pour ce prestataire." });
        }

        var previousStatus = assignment.Status;
        var result = workflow.RefuseMission(session.Provider, assignment, request);
        if (result.Status != ProviderMissionOperationStatus.Ok)
        {
            return ToProviderMissionHttpResult(result);
        }

        var reasonLabel = assignment.RefusalReason?.ToString() ?? "Non renseignee";
        AddProviderAudit(
            db,
            httpRequest,
            session.ProviderId,
            session.Provider.FullName,
            "ProviderMissionRefused",
            nameof(ProviderMissionAssignment),
            assignment.Id,
            "Mission refusee par le prestataire depuis l'application mobile.",
            after: new
            {
                assignment.MissionId,
                AssignmentStatus = assignment.Status,
                assignment.RefusalReason,
                assignment.RefusalComment,
                assignment.RespondedAt
            });

        db.CompanyPortalActivities.Add(new CompanyPortalActivity(
            assignment.CompanyId,
            "mission",
            "Mission refusee",
            $"{session.Provider.FullName} a refuse la mission {assignment.Mission.MissionNumber}. Raison: {reasonLabel}.",
            "orange",
            nameof(ProviderMissionAssignment),
            assignment.Id));

        if (previousStatus != ProviderMissionAssignmentStatus.Refused)
        {
            await notifications.NotifyRefusedAsync(assignment.Mission, session.Provider, assignment, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToProviderMissionHttpResult(result);
    }

    private static async Task<IResult> MarkProviderOnTheWayAsync(
        Guid assignmentId,
        ProviderLocationVerificationRequest request,
        HttpRequest httpRequest,
        IAppDbContext db,
        ProviderMissionWorkflowService workflow,
        ProviderMissionNotificationService notifications,
        CancellationToken cancellationToken)
    {
        var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
        if (session?.Provider is null)
        {
            return Results.Unauthorized();
        }

        var assignment = await db.ProviderMissionAssignments
            .Include(item => item.Mission)
            .FirstOrDefaultAsync(item =>
                item.Id == assignmentId
                && item.ProviderId == session.ProviderId,
                cancellationToken);
        if (assignment?.Mission is null)
        {
            return Results.NotFound(new { message = "Mission introuvable pour ce prestataire." });
        }

        var wasOnTheWay = assignment.Mission.Status == MissionStatus.OnTheWay;
        var result = workflow.MarkOnTheWay(session.Provider, assignment, request);
        if (result.Status != ProviderMissionOperationStatus.Ok)
        {
            return ToProviderMissionHttpResult(result);
        }

        if (!wasOnTheWay && assignment.Mission.Status == MissionStatus.OnTheWay)
        {
            AddProviderAudit(
                db,
                httpRequest,
                session.ProviderId,
                session.Provider.FullName,
                "ProviderOnTheWay",
                nameof(ProviderMissionAssignment),
                assignment.Id,
                "Le prestataire a confirme son depart vers le client depuis l'application mobile.",
                after: new
                {
                    assignment.MissionId,
                    MissionStatus = assignment.Mission.Status
                });
            await notifications.NotifyOnTheWayAsync(
                assignment.Mission,
                session.Provider,
                assignment,
                cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToProviderMissionHttpResult(result);
    }

    private static async Task<IResult> CancelProviderMissionAsync(
        Guid assignmentId,
        CancelMissionRequest request,
        HttpRequest httpRequest,
        IAppDbContext db,
        MissionCancellationWorkflowService cancellationService,
        CancellationToken cancellationToken)
    {
        var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
        if (session?.Provider is null)
        {
            return Results.Unauthorized();
        }

        var assignment = await db.ProviderMissionAssignments
            .AsNoTracking()
            .FirstOrDefaultAsync(assignment =>
                assignment.Id == assignmentId
                && assignment.ProviderId == session.ProviderId,
                cancellationToken);

        if (assignment is null)
        {
            return Results.NotFound(new { message = "Mission introuvable pour ce prestataire." });
        }

        var result = await cancellationService.CancelAsync(
            assignment.MissionId,
            MissionCancellationActor.Provider,
            request,
            expectedCompanyId: assignment.CompanyId,
            expectedProviderId: session.ProviderId,
            cancellationToken);

        if (result.Status == MissionCancellationWorkflowStatus.NotFound)
        {
            return Results.NotFound(new { message = result.Message });
        }

        if (result.Status == MissionCancellationWorkflowStatus.Forbidden)
        {
            return Results.Forbid();
        }

        if (!result.IsSuccess)
        {
            return Results.BadRequest(new { message = result.Message, errors = result.Errors });
        }

        AddProviderAudit(
            db,
            httpRequest,
            session.ProviderId,
            session.Provider.FullName,
            "ProviderMissionCancelled",
            nameof(Mission),
            assignment.MissionId,
            "Mission annulee par le prestataire depuis l'application mobile.",
            before: new { Status = result.PreviousStatus?.ToString() },
            after: result.Response);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(result.Response);
    }

    private static async Task<IResult> VerifyProviderMissionArrivalAsync(
        Guid assignmentId,
        ProviderLocationVerificationRequest request,
        HttpRequest httpRequest,
        IAppDbContext db,
        ProviderMissionWorkflowService workflow,
        ProviderMissionNotificationService notifications,
        CancellationToken cancellationToken)
    {
        var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
        if (session?.Provider is null)
        {
            return Results.Unauthorized();
        }

        var assignment = await db.ProviderMissionAssignments
            .Include(assignment => assignment.Mission)
            .FirstOrDefaultAsync(assignment =>
                assignment.Id == assignmentId
                && assignment.ProviderId == session.ProviderId,
                cancellationToken);

        if (assignment?.Mission is null)
        {
            return Results.NotFound(new { message = "Mission introuvable pour ce prestataire." });
        }

        var wasAlreadyVerified = assignment.HasVerifiedArrival;
        var result = workflow.VerifyArrival(session.Provider, assignment, request);
        if (result.Status != ProviderMissionOperationStatus.Ok)
        {
            return ToProviderMissionHttpResult(result);
        }

        AddProviderAudit(
            db,
            httpRequest,
            session.ProviderId,
            session.Provider.FullName,
            "ProviderArrivalVerified",
            nameof(ProviderMissionAssignment),
            assignment.Id,
            "Arrivee prestataire verifiee depuis l'application mobile.",
            after: new
            {
                assignment.MissionId,
                assignment.ArrivalVerificationStatus,
                assignment.ArrivalVerifiedAt,
                assignment.ArrivalDistanceMeters
            });

        if (!wasAlreadyVerified && assignment.HasVerifiedArrival)
        {
            await notifications.NotifyArrivedAsync(assignment.Mission, session.Provider, assignment, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToProviderMissionHttpResult(result);
    }

    private static async Task<IResult> StartProviderMissionAsync(
        Guid assignmentId,
        ProviderLocationVerificationRequest request,
        HttpRequest httpRequest,
        IAppDbContext db,
        ProviderMissionWorkflowService workflow,
        ProviderMissionNotificationService notifications,
        MissionPaymentMilestoneService milestoneService,
        CancellationToken cancellationToken)
    {
        var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
        if (session?.Provider is null)
        {
            return Results.Unauthorized();
        }

        var assignment = await db.ProviderMissionAssignments
            .Include(assignment => assignment.Mission)
            .FirstOrDefaultAsync(assignment =>
                assignment.Id == assignmentId
                && assignment.ProviderId == session.ProviderId,
                cancellationToken);

        if (assignment?.Mission is null)
        {
            return Results.NotFound(new { message = "Mission introuvable pour ce prestataire." });
        }

        var previousStatus = assignment.Status;
        var result = workflow.StartMission(session.Provider, assignment, request);
        if (result.Status != ProviderMissionOperationStatus.Ok)
        {
            if (result.Response is not null)
            {
                AddProviderAudit(
                    db,
                    httpRequest,
                    session.ProviderId,
                    session.Provider.FullName,
                    "ProviderMissionStartRejected",
                    nameof(ProviderMissionAssignment),
                    assignment.Id,
                    result.Message ?? "Demarrage mission refuse.",
                    after: new
                    {
                        assignment.MissionId,
                        result.Response.Status,
                        result.Response.DistanceMeters
                    });
                await db.SaveChangesAsync(cancellationToken);
            }

            return ToProviderMissionHttpResult(result);
        }

        AddProviderAudit(
            db,
            httpRequest,
            session.ProviderId,
            session.Provider.FullName,
            "ProviderMissionStarted",
            nameof(ProviderMissionAssignment),
            assignment.Id,
            "Mission demarree par le prestataire depuis l'application mobile.",
            after: new
            {
                assignment.MissionId,
                AssignmentStatus = assignment.Status,
                MissionStatus = assignment.Mission.Status,
                assignment.StartedAt
            });
        await milestoneService.EnsureMissionStartedMilestoneAsync(assignment.Mission, cancellationToken);
        db.CompanyPortalActivities.Add(new CompanyPortalActivity(
            assignment.CompanyId,
            "mission",
            "Mission demarree",
            $"{session.Provider.FullName} a demarre la mission {assignment.Mission.MissionNumber}. Les fonds client restent securises.",
            "blue",
            nameof(Mission),
            assignment.MissionId));

        if (previousStatus != ProviderMissionAssignmentStatus.Started)
        {
            await notifications.NotifyStartedAsync(assignment.Mission, session.Provider, assignment, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToProviderMissionHttpResult(result);
    }

    private static async Task<IResult> CompleteProviderMissionAsync(
        Guid assignmentId,
        ProviderCompleteMissionRequest request,
        HttpRequest httpRequest,
        IAppDbContext db,
        ProviderMissionWorkflowService workflow,
        ProviderMissionNotificationService notifications,
        MissionPaymentMilestoneService milestoneService,
        MissionQualityChecklistService qualityService,
        QualityScoringService qualityScoringService,
        CancellationToken cancellationToken)
    {
        var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
        if (session?.Provider is null)
        {
            return Results.Unauthorized();
        }

        var assignment = await db.ProviderMissionAssignments
            .Include(assignment => assignment.Mission)
            .FirstOrDefaultAsync(assignment =>
                assignment.Id == assignmentId
                && assignment.ProviderId == session.ProviderId,
                cancellationToken);

        if (assignment?.Mission is null)
        {
            return Results.NotFound(new { message = "Mission introuvable pour ce prestataire." });
        }

        var qualityGate = await qualityService.ValidateCanCompleteAsync(
            session.ProviderId,
            assignmentId,
            request.QualityExceptionReason,
            cancellationToken);
        if (!qualityGate.IsAllowed)
            return Results.BadRequest(new
            {
                message = qualityGate.Message,
                missingItems = qualityGate.MissingItems,
                qualityGate.CompletedRequiredItemCount,
                qualityGate.RequiredItemCount,
                qualityGate.CompletionPercentage,
                minimumCompletionPercentage = MissionQualityChecklistService.MinimumCompletionPercentage
            });

        var normalizedExceptionReason = string.IsNullOrWhiteSpace(request.QualityExceptionReason)
            ? null
            : request.QualityExceptionReason.Trim();
        if (normalizedExceptionReason is { Length: > 1200 })
        {
            normalizedExceptionReason = normalizedExceptionReason[..1200];
        }

        var completionRequest = qualityGate.UsedException
            ? request with
            {
                Note = string.Join(
                    Environment.NewLine,
                    new[]
                    {
                        request.Note?.Trim(),
                        $"Motif exceptionnel checklist ({qualityGate.CompletionPercentage} %) : {normalizedExceptionReason}"
                    }.Where(value => !string.IsNullOrWhiteSpace(value)))
            }
            : request;

        var previousStatus = assignment.Status;
        var customerValidationWindow = await MissionWorkflowSettingsResolver.ResolveMinutesAsync(
            db,
            MissionWorkflowSettingsResolver.CustomerCompletionValidationMinutes,
            120,
            cancellationToken);
        var hasBlockingAdditionalQuote = await db.MissionAdditionalQuotes
            .AsNoTracking()
            .AnyAsync(item => item.MissionId == assignment.MissionId
                && (item.Status == MissionAdditionalQuoteStatus.Requested
                    || item.Status == MissionAdditionalQuoteStatus.Submitted), cancellationToken);
        var result = workflow.CompleteMission(
            session.Provider,
            assignment,
            completionRequest,
            DateTimeOffset.UtcNow.Add(customerValidationWindow),
            hasBlockingAdditionalQuote);
        if (result.Status != ProviderMissionOperationStatus.Ok)
        {
            return ToProviderMissionHttpResult(result);
        }

        AddProviderAudit(
            db,
            httpRequest,
            session.ProviderId,
            session.Provider.FullName,
            "ProviderMissionCompleted",
            nameof(ProviderMissionAssignment),
            assignment.Id,
            "Mission terminee par le prestataire depuis l'application mobile.",
            after: new
            {
                assignment.MissionId,
                AssignmentStatus = assignment.Status,
                MissionStatus = assignment.Mission.Status,
                request.ActualDurationMinutes,
                assignment.CompletedAt,
                ChecklistCompletionPercentage = qualityGate.CompletionPercentage,
                ChecklistExceptionUsed = qualityGate.UsedException,
                ChecklistExceptionReason = qualityGate.UsedException ? normalizedExceptionReason : null
            });

        await milestoneService.EnsureMissionCompletedMilestoneAsync(assignment.Mission, cancellationToken);
        db.CompanyPortalActivities.Add(new CompanyPortalActivity(
            assignment.CompanyId,
            "mission",
            "Mission terminee",
            $"{session.Provider.FullName} a termine la mission {assignment.Mission.MissionNumber}. Paiement entreprise a liberer apres validation client.",
            "green",
            nameof(Mission),
            assignment.MissionId));

        if (previousStatus != ProviderMissionAssignmentStatus.Completed)
        {
            await notifications.NotifyCompletedAsync(assignment.Mission, session.Provider, assignment, cancellationToken);
        }

        await qualityService.LockAfterCompletionAsync(assignment.MissionId, cancellationToken);
        await qualityScoringService.EnsureCompletionAuditAndScoresAsync(assignment.Mission, assignment, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return ToProviderMissionHttpResult(result);
    }

    private static void AddProviderAudit(
        IAppDbContext db,
        HttpRequest httpRequest,
        Guid providerId,
        string? providerName,
        string action,
        string entityType,
        Guid? entityId,
        string summary,
        object? before = null,
        object? after = null)
    {
        db.AuditLogEntries.Add(AuditLogFactory.Create(
            AuditActor.Provider(providerId, providerName),
            action,
            entityType,
            entityId,
            summary,
            HttpAuditContextFactory.Create(httpRequest),
            before,
            after));
    }

    private static ProviderMobileProfileCompletionResponse? BuildProviderMobileProfileCompletion(ProviderProfile provider)
    {
        var missing = new List<string>();
        if (!provider.Documents.Any(document => document.DocumentType == ProviderDocumentType.Photo))
        {
            missing.Add("Photo de profil");
        }

        if (!provider.Documents.Any(document => document.DocumentType == ProviderDocumentType.IdentityDocument))
        {
            missing.Add("Piece d'identite");
        }

        if (!provider.Services.Any(service => service.IsActive))
        {
            missing.Add("Service actif");
        }

        if (provider.MissionLatitude is null || provider.MissionLongitude is null)
        {
            missing.Add("Zone de mission");
        }

        if (missing.Count == 0)
        {
            return null;
        }

        var percent = Math.Clamp(100 - missing.Count * 8, 0, 99);
        var message = missing.Count == 1
            ? $"Completez : {missing[0]}."
            : $"Completez {missing.Count} elements pour recevoir toutes les affectations.";

        return new ProviderMobileProfileCompletionResponse(percent, message, missing);
    }

    private static ProviderMobileMissionSummaryResponse? ToProviderMobileMissionSummary(
        ProviderMissionAssignment assignment,
        IReadOnlyDictionary<Guid, Service> servicesById,
        IReadOnlyDictionary<Guid, CustomerProfile> customersById,
        int unreadMessageCount = 0)
    {
        if (assignment.Mission is null)
        {
            return null;
        }

        servicesById.TryGetValue(assignment.Mission.ServiceId, out var service);
        customersById.TryGetValue(assignment.Mission.CustomerId, out var customer);
        var isClosed = assignment.Status is ProviderMissionAssignmentStatus.Completed
                or ProviderMissionAssignmentStatus.Cancelled
                or ProviderMissionAssignmentStatus.Refused
                or ProviderMissionAssignmentStatus.Expired
            || assignment.Mission.Status is MissionStatus.Completed
                or MissionStatus.Cancelled
                or MissionStatus.Disputed
                or MissionStatus.Resolved;
        var effectiveStatus = assignment.Mission.Status switch
        {
            MissionStatus.Completed or MissionStatus.Resolved => "Completed",
            MissionStatus.Cancelled or MissionStatus.Disputed => "Cancelled",
            MissionStatus.OnTheWay => "OnTheWay",
            MissionStatus.Started => "Started",
            MissionStatus.Accepted => "Accepted",
            _ => assignment.Status.ToString()
        };
        var canCallCustomer = !isClosed && assignment.Mission.CanRevealContactDetails && customer is not null;
        return new ProviderMobileMissionSummaryResponse(
            assignment.Id,
            assignment.MissionId,
            assignment.Mission.MissionNumber,
            service?.Name ?? "Service",
            service?.IconName ?? "sparkles",
            assignment.Mission.ServicePrestation?.Name,
            assignment.Company?.Name ?? "Entreprise",
            isClosed ? "Adresse masquee apres la mission" : BuildLocationLabel(assignment.Mission.ServiceAddress),
            assignment.Mission.ScheduledFor,
            effectiveStatus,
            canCallCustomer,
            canCallCustomer ? customer!.PhoneNumber : null,
            unreadMessageCount);
    }

    private static ProviderMobileMissionOfferResponse? ToProviderMobileMissionOffer(
        ProviderMissionAssignment assignment,
        ProviderProfile provider,
        DateTimeOffset now,
        IReadOnlyDictionary<Guid, Service> servicesById,
        IReadOnlyDictionary<Guid, CustomerProfile> customersById)
    {
        if (assignment.Mission is null)
        {
            return null;
        }

        servicesById.TryGetValue(assignment.Mission.ServiceId, out var service);
        customersById.TryGetValue(assignment.Mission.CustomerId, out var customer);
        var distanceKm = CalculateDistanceKm(
            provider.CurrentLatitude ?? provider.MissionLatitude,
            provider.CurrentLongitude ?? provider.MissionLongitude,
            assignment.Mission.ServiceLatitude,
            assignment.Mission.ServiceLongitude);

        return new ProviderMobileMissionOfferResponse(
            assignment.Id,
            assignment.MissionId,
            assignment.Mission.MissionNumber,
            service?.Name ?? "Service",
            service?.IconName ?? "sparkles",
            assignment.Company?.Name ?? provider.Company?.Name ?? "Entreprise",
            BuildCustomerDisplayName(customer),
            BuildLocationLabel(assignment.Mission.ServiceAddress),
            distanceKm,
            distanceKm is null ? null : Math.Max(1, (int)Math.Round(distanceKm.Value / 18d * 60d)),
            assignment.ExpiresAt,
            Math.Max(0, (int)Math.Floor((assignment.ExpiresAt - now).TotalSeconds)),
            "Verifiez que vous pouvez partir maintenant avant d'accepter.");
    }

    private static string BuildLocationLabel(string? address)
    {
        return string.IsNullOrWhiteSpace(address) ? "Adresse a confirmer" : address.Trim();
    }

    private static string? GetOptionalFormValue(IFormCollection form, string key)
    {
        return form.TryGetValue(key, out var value) ? value.ToString() : null;
    }

    private static bool TryParseProviderDocumentType(string? value, out ProviderDocumentType documentType)
    {
        return Enum.TryParse(value, true, out documentType)
            && documentType is ProviderDocumentType.Photo or ProviderDocumentType.IdentityDocument or ProviderDocumentType.Diploma;
    }

    private static async Task TryDeleteProviderFileAsync(
        CompanyProviderUploadService uploadService,
        string storagePath,
        CancellationToken cancellationToken)
    {
        try
        {
            await uploadService.DeleteIfExistsAsync(storagePath, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Best effort cleanup only; upload failure is already reported to the caller.
        }
    }

    private static string BuildCustomerDisplayName(CustomerProfile? customer)
    {
        if (customer is null)
        {
            return "Client";
        }

        var displayName = $"{customer.FirstName} {customer.LastName}".Trim();
        return string.IsNullOrWhiteSpace(displayName) ? "Client" : displayName;
    }

    private static double? CalculateDistanceKm(decimal? fromLatitude, decimal? fromLongitude, decimal? toLatitude, decimal? toLongitude)
    {
        if (fromLatitude is null || fromLongitude is null || toLatitude is null || toLongitude is null)
        {
            return null;
        }

        const double earthRadiusKm = 6371d;
        var latA = DegreesToRadians((double)fromLatitude.Value);
        var latB = DegreesToRadians((double)toLatitude.Value);
        var deltaLatitude = DegreesToRadians((double)(toLatitude.Value - fromLatitude.Value));
        var deltaLongitude = DegreesToRadians((double)(toLongitude.Value - fromLongitude.Value));
        var haversine = Math.Sin(deltaLatitude / 2) * Math.Sin(deltaLatitude / 2)
            + Math.Cos(latA) * Math.Cos(latB) * Math.Sin(deltaLongitude / 2) * Math.Sin(deltaLongitude / 2);
        var centralAngle = 2 * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(1 - haversine));
        return Math.Round(earthRadiusKm * centralAngle, 1);
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180d;
    }

    private static async Task<ProviderPortalSession?> GetProviderPortalSessionAsync(
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

        var tokenHash = PortalTokenService.HashToken(token);
        return await db.ProviderPortalSessions
            .Include(session => session.Provider)
            .ThenInclude(provider => provider!.Company)
            .FirstOrDefaultAsync(session => session.TokenHash == tokenHash && session.RevokedAt == null && session.ExpiresAt > DateTimeOffset.UtcNow, cancellationToken);
    }

    private static IResult ToProviderMissionHttpResult(ProviderMissionOperationResult result)
    {
        return result.Status switch
        {
            ProviderMissionOperationStatus.Ok => Results.Ok(result.Response),
            ProviderMissionOperationStatus.Forbidden => Results.Forbid(),
            ProviderMissionOperationStatus.NotFound => Results.NotFound(new { message = result.Message }),
            ProviderMissionOperationStatus.BadRequest => result.Response is null
                ? Results.BadRequest(new { message = result.Message })
                : Results.BadRequest(result.Response),
            _ => Results.BadRequest(new { message = result.Message ?? "Action impossible." })
        };
    }
}
