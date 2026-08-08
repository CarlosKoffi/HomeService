using HomeService.Application.Abstractions;
using HomeService.Application.Missions;
using HomeService.Contracts.ProviderPortal;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.ProviderPortal;

public sealed class ProviderMobileMissionDetailService(IAppDbContext db)
{
    public async Task<ProviderMobileMissionDetailResult> GetAsync(
        Guid providerId,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var assignment = await db.ProviderMissionAssignments
            .AsNoTracking()
            .Include(item => item.Company)
            .Include(item => item.Mission)
            .FirstOrDefaultAsync(item => item.Id == assignmentId && item.ProviderId == providerId, cancellationToken);
        if (assignment?.Mission is null)
        {
            return ProviderMobileMissionDetailResult.NotFound("Mission introuvable pour ce prestataire.");
        }

        var provider = await db.Providers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == providerId, cancellationToken);
        if (provider is null)
        {
            return ProviderMobileMissionDetailResult.Forbidden("Session prestataire invalide.");
        }

        var mission = assignment.Mission;
        var isClosed = IsClosedForProvider(assignment.Status, mission.Status);
        var service = await db.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == mission.ServiceId, cancellationToken);
        var prestation = mission.ServicePrestationId is null
            ? null
            : await db.ServicePrestations
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == mission.ServicePrestationId.Value, cancellationToken);
        var customer = isClosed
            ? null
            : await db.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == mission.CustomerId, cancellationToken);

        IReadOnlyList<ProviderMobileMissionPhotoResponse> photos = isClosed
            ? []
            : await db.MissionAttachments
                .AsNoTracking()
                .Where(attachment => attachment.MissionId == mission.Id
                    && attachment.AttachmentType == MissionAttachmentType.CustomerPhoto
                    && !attachment.IsDeleted)
                .OrderBy(attachment => attachment.CreatedAt)
                .Select(attachment => new ProviderMobileMissionPhotoResponse(
                    attachment.Id,
                    attachment.OriginalFileName,
                    attachment.StoragePath,
                    attachment.ContentType,
                    attachment.Caption))
                .ToListAsync(cancellationToken);

        var conversationIds = !MissionConversationAccessPolicy.CanAccess(mission.Status) ? [] : await db.MissionConversations
            .AsNoTracking()
            .Where(conversation => conversation.MissionId == mission.Id
                && (conversation.ProviderId == null || conversation.ProviderId == providerId))
            .Select(conversation => conversation.Id)
            .ToListAsync(cancellationToken);

        var messages = isClosed || conversationIds.Count == 0
            ? []
            : await db.MissionMessages
                .AsNoTracking()
                .Where(message => conversationIds.Contains(message.ConversationId))
                .OrderByDescending(message => message.CreatedAt)
                .Take(5)
                .Select(message => new ProviderMobileMissionMessageResponse(
                    message.Id,
                    message.SenderType.ToString(),
                    message.Body,
                    message.AttachmentPath,
                    message.AttachmentContentType,
                    message.CreatedAt,
                    message.ReadAt))
                .ToListAsync(cancellationToken);

        IReadOnlyList<ProviderMobileMissionAdditionalQuoteResponse> additionalQuotes = isClosed
            ? []
            : await db.MissionAdditionalQuotes
                .AsNoTracking()
                .Where(quote => quote.MissionId == mission.Id && quote.ProviderId == providerId)
                .OrderByDescending(quote => quote.RequestedAt)
                .Select(quote => new ProviderMobileMissionAdditionalQuoteResponse(
                    quote.Id,
                    quote.Status.ToString(),
                    quote.Reason,
                    quote.RequestedPhotoStoragePath,
                    quote.CompanyDescription,
                    quote.RequestedAt,
                    quote.SubmittedAt,
                    quote.PaidAt))
                .ToListAsync(cancellationToken);

        var hasBlockingAdditionalQuote = additionalQuotes.Any(quote =>
            quote.Status is nameof(MissionAdditionalQuoteStatus.Requested)
                or nameof(MissionAdditionalQuoteStatus.Submitted));
        var canCallCustomer = !isClosed && mission.CanRevealContactDetails && customer is not null;
        var now = DateTimeOffset.UtcNow;
        var effectiveAssignmentStatus = mission.Status switch
        {
            MissionStatus.Completed or MissionStatus.Resolved => "Completed",
            MissionStatus.Cancelled or MissionStatus.Disputed => "Cancelled",
            MissionStatus.OnTheWay => "OnTheWay",
            MissionStatus.Started => "Started",
            MissionStatus.Accepted => "Accepted",
            _ => assignment.Status.ToString()
        };
        var response = new ProviderMobileMissionDetailResponse(
            assignment.Id,
            mission.Id,
            mission.MissionNumber,
            effectiveAssignmentStatus,
            mission.Status.ToString(),
            service?.Name ?? "Service",
            service?.IconName ?? "sparkles",
            prestation?.Name,
            assignment.Company?.Name ?? "Entreprise",
            isClosed ? "Client" : BuildCustomerDisplayName(customer),
            canCallCustomer ? customer!.PhoneNumber : null,
            canCallCustomer,
            isClosed ? "Adresse masquee apres la mission" : BuildLocationLabel(mission.ServiceAddress),
            isClosed ? null : mission.ServiceLatitude,
            isClosed ? null : mission.ServiceLongitude,
            isClosed ? null : CalculateDistanceKm(
                provider.CurrentLatitude ?? provider.MissionLatitude,
                provider.CurrentLongitude ?? provider.MissionLongitude,
                mission.ServiceLatitude,
                mission.ServiceLongitude),
            mission.ScheduledFor,
            assignment.ExpiresAt,
            Math.Max(0, (int)Math.Floor((assignment.ExpiresAt - now).TotalSeconds)),
            isClosed ? null : mission.PartsDescription,
            mission.Description,
            BuildActions(assignment, mission, hasBlockingAdditionalQuote),
            new ProviderMobileMissionArrivalResponse(
                assignment.ArrivalVerificationStatus.ToString(),
                assignment.HasVerifiedArrival,
                isClosed ? null : assignment.ArrivalDistanceMeters,
                assignment.ArrivalToleranceMeters,
                isClosed ? null : assignment.ArrivalAccuracyMeters,
                isClosed ? null : assignment.ArrivalVerifiedAt),
            additionalQuotes,
            photos,
            messages
                .OrderBy(message => message.CreatedAt)
                .ToList());

        return ProviderMobileMissionDetailResult.Ok(response);
    }

    private static ProviderMobileMissionActionsResponse BuildActions(
        Domain.Entities.ProviderMissionAssignment assignment,
        Domain.Entities.Mission mission,
        bool hasBlockingAdditionalQuote)
    {
        return new ProviderMobileMissionActionsResponse(
            assignment.Status == ProviderMissionAssignmentStatus.Offered && assignment.ExpiresAt > DateTimeOffset.UtcNow,
            assignment.Status == ProviderMissionAssignmentStatus.Offered && assignment.ExpiresAt > DateTimeOffset.UtcNow,
            assignment.Status == ProviderMissionAssignmentStatus.Accepted
                && mission.Status == MissionStatus.Accepted
                && mission.IsInitialPaymentConfirmed,
            assignment.Status == ProviderMissionAssignmentStatus.Accepted
                && mission.Status == MissionStatus.Accepted
                && mission.IsInitialPaymentConfirmed
                && mission.CustomerConfirmedAt.HasValue
                    ? mission.CustomerConfirmedAt.Value + ProviderDepartureAutomationService.DepartureGracePeriod
                    : null,
            assignment.Status == ProviderMissionAssignmentStatus.Accepted
                && mission.IsInitialPaymentConfirmed,
            assignment.Status == ProviderMissionAssignmentStatus.Accepted
                && mission.IsInitialPaymentConfirmed
                && assignment.HasVerifiedArrival
                && mission.CanStartFor(assignment.ProviderId, assignment.CompanyId),
            assignment.Status == ProviderMissionAssignmentStatus.Started && !hasBlockingAdditionalQuote,
            assignment.Status is ProviderMissionAssignmentStatus.Offered or ProviderMissionAssignmentStatus.Accepted or ProviderMissionAssignmentStatus.Started);
    }

    private static bool IsClosedForProvider(
        ProviderMissionAssignmentStatus assignmentStatus,
        MissionStatus missionStatus)
    {
        return assignmentStatus is ProviderMissionAssignmentStatus.Completed
                or ProviderMissionAssignmentStatus.Cancelled
                or ProviderMissionAssignmentStatus.Refused
                or ProviderMissionAssignmentStatus.Expired
            || missionStatus is MissionStatus.Completed
                or MissionStatus.Cancelled
                or MissionStatus.Disputed
                or MissionStatus.Resolved;
    }

    private static string BuildLocationLabel(string? address)
    {
        return string.IsNullOrWhiteSpace(address) ? "Adresse a confirmer" : address.Trim();
    }

    private static string BuildCustomerDisplayName(Domain.Entities.CustomerProfile? customer)
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
}

public sealed record ProviderMobileMissionDetailResult(
    ProviderMobileMissionDetailResultStatus Status,
    ProviderMobileMissionDetailResponse? Response,
    string Message)
{
    public bool IsSuccess => Status == ProviderMobileMissionDetailResultStatus.Success;

    public static ProviderMobileMissionDetailResult Ok(ProviderMobileMissionDetailResponse response)
    {
        return new ProviderMobileMissionDetailResult(ProviderMobileMissionDetailResultStatus.Success, response, string.Empty);
    }

    public static ProviderMobileMissionDetailResult NotFound(string message)
    {
        return new ProviderMobileMissionDetailResult(ProviderMobileMissionDetailResultStatus.NotFound, null, message);
    }

    public static ProviderMobileMissionDetailResult Forbidden(string message)
    {
        return new ProviderMobileMissionDetailResult(ProviderMobileMissionDetailResultStatus.Forbidden, null, message);
    }
}

public enum ProviderMobileMissionDetailResultStatus
{
    Success = 0,
    NotFound = 1,
    Forbidden = 2
}
