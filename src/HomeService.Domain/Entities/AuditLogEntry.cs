using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class AuditLogEntry : AuditableEntity
{
    private AuditLogEntry()
    {
    }

    public AuditLogEntry(
        AuditActorType actorType,
        Guid? actorId,
        string? actorDisplayName,
        string action,
        string entityType,
        Guid? entityId,
        string? summary,
        string? beforeJson,
        string? afterJson,
        string? ipAddress,
        string? userAgent,
        string? correlationId)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Audit action is required.", nameof(action));
        }

        if (string.IsNullOrWhiteSpace(entityType))
        {
            throw new ArgumentException("Audit entity type is required.", nameof(entityType));
        }

        ActorType = actorType;
        ActorId = actorId;
        ActorDisplayName = actorDisplayName?.Trim();
        Action = action.Trim();
        EntityType = entityType.Trim();
        EntityId = entityId;
        Summary = summary?.Trim();
        BeforeJson = beforeJson?.Trim();
        AfterJson = afterJson?.Trim();
        IpAddress = ipAddress?.Trim();
        UserAgent = userAgent?.Trim();
        CorrelationId = correlationId?.Trim();
        OccurredAt = DateTimeOffset.UtcNow;
    }

    public AuditActorType ActorType { get; private set; }
    public Guid? ActorId { get; private set; }
    public string? ActorDisplayName { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public Guid? EntityId { get; private set; }
    public string? Summary { get; private set; }
    public string? BeforeJson { get; private set; }
    public string? AfterJson { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? CorrelationId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
}
