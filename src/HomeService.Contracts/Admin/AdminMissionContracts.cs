namespace HomeService.Contracts.Admin;

public sealed record AdminMissionListResponse(
    IReadOnlyList<AdminMissionSummaryResponse> Items,
    AdminMissionStatsResponse Stats);

public sealed record AdminMissionStatsResponse(
    int TotalMissions,
    int OpenMissions,
    int ScheduledMissions,
    int CompletedMissions,
    int DisputedMissions);

public sealed record AdminMissionSummaryResponse(
    Guid Id,
    string ServiceName,
    string? CompanyName,
    string CustomerName,
    string CustomerPhoneNumber,
    string? ProviderName,
    string Status,
    string PaymentStatus,
    string PaymentMethod,
    DateTimeOffset? ScheduledFor,
    int? Amount,
    string Currency,
    string? ServiceAddress,
    DateTimeOffset CreatedAt);

public sealed record AdminMissionActionRequest(string? Note);
