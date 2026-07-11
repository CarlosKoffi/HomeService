namespace HomeService.Contracts.Monitoring;

public sealed record AuditLogListResponse(
    int Total,
    int Skip,
    int Take,
    IReadOnlyList<AuditLogEntryResponse> Items);
