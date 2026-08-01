using HomeService.Domain.Enums;

namespace HomeService.Application.Clients;

public static class ClientMissionDuplicatePolicy
{
    public static bool IsDuplicate(
        Guid requestedServiceId,
        Guid? requestedPrestationId,
        string? requestedAddress,
        MissionMode requestedMode,
        DateTimeOffset? requestedScheduledFor,
        ExistingClientMission candidate)
    {
        if (!IsActive(candidate.Status) || candidate.Mode != requestedMode)
        {
            return false;
        }

        if (requestedMode == MissionMode.Scheduled
            && !IsSameAppointment(requestedScheduledFor, candidate.ScheduledFor))
        {
            return false;
        }

        if (candidate.ServiceId != requestedServiceId
            || !AddressesMatch(requestedAddress, candidate.ServiceAddress))
        {
            return false;
        }

        return requestedPrestationId is null
            || candidate.ServicePrestationId is null
            || requestedPrestationId == candidate.ServicePrestationId;
    }

    private static bool IsActive(MissionStatus status)
    {
        return status is MissionStatus.Created
            or MissionStatus.SearchingProvider
            or MissionStatus.Offered
            or MissionStatus.Assigned
            or MissionStatus.Accepted
            or MissionStatus.OnTheWay
            or MissionStatus.Started;
    }

    private static bool AddressesMatch(string? first, string? second)
    {
        return !string.IsNullOrWhiteSpace(first)
            && !string.IsNullOrWhiteSpace(second)
            && string.Equals(Normalize(first), Normalize(second), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameAppointment(DateTimeOffset? first, DateTimeOffset? second)
    {
        return first.HasValue
            && second.HasValue
            && Math.Abs((first.Value.ToUniversalTime() - second.Value.ToUniversalTime()).TotalMinutes) < 1;
    }

    private static string Normalize(string value)
    {
        return string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}

public sealed record ExistingClientMission(
    string MissionNumber,
    Guid ServiceId,
    Guid? ServicePrestationId,
    MissionMode Mode,
    MissionStatus Status,
    string? ServiceAddress,
    DateTimeOffset? ScheduledFor);
