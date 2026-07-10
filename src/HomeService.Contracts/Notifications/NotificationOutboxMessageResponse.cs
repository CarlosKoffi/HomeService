namespace HomeService.Contracts.Notifications;

public sealed record NotificationOutboxMessageResponse(
    Guid Id,
    string Channel,
    string Status,
    string Recipient,
    string Subject,
    string Body,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    DateTimeOffset ScheduledAt,
    DateTimeOffset? SentAt,
    string? FailureReason);
