namespace HomeService.Application.Auditing;

public sealed record AuditRequestContext(string? IpAddress, string? UserAgent, string? CorrelationId);
