namespace HomeService.Contracts.Missions;

public sealed record RequestMissionAdditionalQuoteRequest(
    string Reason,
    string? PhotoStoragePath);

public sealed record SubmitMissionAdditionalQuoteRequest(
    int Amount,
    string Currency,
    string Description);

public sealed record PayMissionAdditionalQuoteRequest(
    string PhoneNumber,
    string? PaymentReference);

public sealed record MissionAdditionalQuoteResponse(
    Guid Id,
    Guid MissionId,
    string MissionNumber,
    string Status,
    string Reason,
    string? PhotoStoragePath,
    int? Amount,
    string Currency,
    string? Description,
    DateTimeOffset RequestedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? PaidAt);
