using HomeService.Application.Auditing;

namespace HomeService.Api.Auditing;

public static class HttpAuditContextFactory
{
    public static AuditRequestContext Create(HttpRequest request)
    {
        var correlationId = request.Headers.TryGetValue("X-Correlation-Id", out var headerValue)
            ? headerValue.ToString()
            : request.HttpContext.TraceIdentifier;

        var ipAddress = request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor)
            ? forwardedFor.ToString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            : request.HttpContext.Connection.RemoteIpAddress?.ToString();

        return new AuditRequestContext(
            ipAddress,
            request.Headers.UserAgent.ToString(),
            correlationId);
    }
}
