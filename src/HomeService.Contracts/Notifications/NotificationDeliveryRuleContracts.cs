namespace HomeService.Contracts.Notifications;

public sealed record NotificationDeliveryRuleResponse(
    Guid Id,
    string EventKey,
    string EventGroup,
    string Label,
    string Audience,
    string AudienceGroup,
    bool PortalEnabled,
    bool MobileAppEnabled,
    bool EmailEnabled,
    bool WhatsAppEnabled,
    string? SubjectTemplate,
    string? BodyTemplate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record UpdateNotificationDeliveryRuleRequest(
    string Label,
    string Audience,
    bool PortalEnabled,
    bool MobileAppEnabled,
    bool EmailEnabled,
    bool WhatsAppEnabled,
    string? SubjectTemplate,
    string? BodyTemplate);
