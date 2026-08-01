namespace HomeService.Contracts.Clients;

public sealed record ClientMissionListItemResponse(
    Guid MissionId,
    string MissionNumber,
    string Status,
    string QuoteStatus,
    string PaymentStatus,
    string? ServiceName,
    string? PrestationName,
    string? OptionName,
    string? ServiceAddress,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ScheduledFor,
    int? Amount,
    string Currency,
    string PrimaryAction,
    string? IconUrl = null);
