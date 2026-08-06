namespace HomeService.Contracts.CompanyPortal;

public sealed record CompanyMissionOfferResponse(
    Guid? OfferId,
    Guid MissionId,
    string MissionNumber,
    string ServiceName,
    string CustomerName,
    string CustomerPhoneNumber,
    string Status,
    DateTimeOffset ExpiresAt,
    string? ServiceAddress,
    string? Description,
    int EstimatedDurationMinutes,
    DateTimeOffset? ScheduledFor,
    int? Rank,
    int? Score,
    bool CanAccept,
    bool HasCompatibleProvider,
    string AccessState,
    string AccessMessage,
    int CompanyPriority,
    bool CanRefuse = false);
