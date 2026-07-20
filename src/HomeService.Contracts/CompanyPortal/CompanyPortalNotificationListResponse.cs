namespace HomeService.Contracts.CompanyPortal;

public sealed record CompanyPortalNotificationListResponse(
    int UnreadCount,
    IReadOnlyList<CompanyPortalNotificationResponse> Notifications);
