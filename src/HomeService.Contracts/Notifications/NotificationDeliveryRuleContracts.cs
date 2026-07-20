namespace HomeService.Contracts.Notifications;

public sealed record NotificationDeliveryRuleResponse(
    Guid Id,
    string EventKey,
    string Label,
    string Audience,
    bool PortalEnabled,
    bool MobileAppEnabled,
    bool EmailEnabled,
    bool WhatsAppEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record UpdateNotificationDeliveryRuleRequest(
    string Label,
    string Audience,
    bool PortalEnabled,
    bool MobileAppEnabled,
    bool EmailEnabled,
    bool WhatsAppEnabled);
