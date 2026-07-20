using HomeService.Api.Auditing;
using HomeService.Application.Abstractions;
using HomeService.Application.Auditing;
using HomeService.Application.ProviderPortal;
using HomeService.Application.Security;
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
        .WithName("ActivateProviderInvitation");

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
        .WithName("LoginProviderPortal");

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
                .Where(assignment =>
                    assignment.ProviderId == provider.Id
                    && assignment.Status != ProviderMissionAssignmentStatus.Refused
                    && assignment.Status != ProviderMissionAssignmentStatus.Completed
                    && assignment.Status != ProviderMissionAssignmentStatus.Expired)
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
                .Where(assignment => assignment.Status != ProviderMissionAssignmentStatus.Offered || assignment.ExpiresAt <= now)
                .OrderBy(assignment => assignment.Mission!.ScheduledFor ?? assignment.ExpiresAt)
                .Select(assignment => ToProviderMobileMissionSummary(assignment, servicesById, customersById))
                .FirstOrDefault();

            return Results.Ok(new ProviderMobileHomeResponse(
                new ProviderMobileStatusResponse(
                    provider.FullName,
                    provider.Company?.Name ?? "En attente d'entreprise",
                    provider.IsAvailable,
                    provider.IsAvailable ? "Disponible" : "Indisponible",
                    provider.MissionRadiusKm),
                BuildProviderMobileProfileCompletion(provider),
                upcomingMission,
                liveOffer));
        })
        .WithName("GetProviderMobileHome");

        group.MapPost("/mission-assignments/{assignmentId:guid}/accept", async (
            Guid assignmentId,
            ProviderAcceptMissionRequest request,
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

            var result = workflow.AcceptMission(session.Provider, assignment, request);
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
            await db.SaveChangesAsync(cancellationToken);
            return ToProviderMissionHttpResult(result);
        })
        .WithName("AcceptProviderMission");

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
            await db.SaveChangesAsync(cancellationToken);
            return ToProviderMissionHttpResult(result);
        })
        .WithName("StartProviderMissionWithArrivalVerification");

        return app;
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
        IReadOnlyDictionary<Guid, CustomerProfile> customersById)
    {
        if (assignment.Mission is null)
        {
            return null;
        }

        servicesById.TryGetValue(assignment.Mission.ServiceId, out var service);
        customersById.TryGetValue(assignment.Mission.CustomerId, out var customer);
        var canCallCustomer = assignment.Mission.CanRevealContactDetails && customer is not null;
        return new ProviderMobileMissionSummaryResponse(
            assignment.Id,
            assignment.MissionId,
            assignment.Mission.MissionNumber,
            service?.Name ?? "Service",
            service?.IconName ?? "sparkles",
            assignment.Company?.Name ?? "Entreprise",
            BuildLocationLabel(assignment.Mission.ServiceAddress),
            assignment.Mission.ScheduledFor,
            assignment.Status.ToString(),
            canCallCustomer,
            canCallCustomer ? customer!.PhoneNumber : null);
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
