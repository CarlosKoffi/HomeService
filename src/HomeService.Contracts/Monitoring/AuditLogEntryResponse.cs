namespace HomeService.Contracts.Monitoring;

public sealed record AuditLogEntryResponse(
    Guid Id,
    string ActorType,
    Guid? ActorId,
    string? ActorDisplayName,
    string Action,
    string EntityType,
    Guid? EntityId,
    string? Summary,
    string? BeforeJson,
    string? AfterJson,
    string? IpAddress,
    string? UserAgent,
    string? CorrelationId,
    DateTimeOffset OccurredAt);
