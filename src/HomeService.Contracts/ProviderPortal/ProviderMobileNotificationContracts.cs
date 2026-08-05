namespace HomeService.Contracts.ProviderPortal;

public sealed record ProviderMobileNotificationListResponse(
    int UnreadCount,
    IReadOnlyList<ProviderMobileNotificationResponse> Items);

public sealed record ProviderMobileNotificationResponse(
    Guid Id,
    string Title,
    string Body,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    DateTimeOffset CreatedAt,
    bool IsRead);
