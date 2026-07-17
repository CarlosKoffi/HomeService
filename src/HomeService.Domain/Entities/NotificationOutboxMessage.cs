using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class NotificationOutboxMessage : AuditableEntity
{
    private NotificationOutboxMessage()
    {
    }

    public NotificationOutboxMessage(
        NotificationChannel channel,
        string recipient,
        string subject,
        string body,
        string? relatedEntityType,
        Guid? relatedEntityId,
        string? metadataJson = null)
    {
        Channel = channel;
        Recipient = recipient.Trim();
        Subject = subject.Trim();
        Body = body.Trim();
        RelatedEntityType = relatedEntityType?.Trim();
        RelatedEntityId = relatedEntityId;
        MetadataJson = metadataJson?.Trim();
        Status = NotificationStatus.Pending;
        ScheduledAt = DateTimeOffset.UtcNow;
    }

    public NotificationChannel Channel { get; private set; }
    public NotificationStatus Status { get; private set; }
    public string Recipient { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string? RelatedEntityType { get; private set; }
    public Guid? RelatedEntityId { get; private set; }
    public string? MetadataJson { get; private set; }
    public DateTimeOffset ScheduledAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public string? FailureReason { get; private set; }

    public void MarkSent()
    {
        Status = NotificationStatus.Sent;
        SentAt = DateTimeOffset.UtcNow;
        FailureReason = null;
        Touch();
    }

    public void MarkFailed(string reason)
    {
        Status = NotificationStatus.Failed;
        FailureReason = reason.Trim();
        Touch();
    }

    public void Retry()
    {
        if (Status is NotificationStatus.Sent)
        {
            throw new InvalidOperationException("Une notification deja envoyee ne peut pas etre relancee.");
        }

        Status = NotificationStatus.Pending;
        FailureReason = null;
        ScheduledAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void Cancel(string? reason = null)
    {
        if (Status is NotificationStatus.Sent)
        {
            throw new InvalidOperationException("Une notification deja envoyee ne peut pas etre annulee.");
        }

        Status = NotificationStatus.Cancelled;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        Touch();
    }
}
