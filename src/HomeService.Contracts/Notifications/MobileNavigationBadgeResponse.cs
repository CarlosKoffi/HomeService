namespace HomeService.Contracts.Notifications;

public sealed record MobileNavigationBadgeResponse(
    int ActionCount,
    int MessageCount,
    int AlertCount);
