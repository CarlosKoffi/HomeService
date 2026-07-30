namespace HomeService.Contracts.Clients;

public sealed record ClientNotificationListResponse(
    int UnreadCount,
    IReadOnlyList<ClientNotificationResponse> Notifications);

public sealed record ClientNotificationUnreadCountResponse(
    int UnreadCount);

public sealed record ClientNotificationResponse(
    Guid Id,
    string Title,
    string Body,
    string Status,
    bool IsRead,
    DateTimeOffset ScheduledAt,
    DateTimeOffset? SentAt,
    DateTimeOffset? ReadAt,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    string? MetadataJson);
