namespace HomeService.Contracts.Notifications;

public sealed record NotificationTemplateResponse(
    Guid Id,
    string EventKey,
    string Channel,
    string EventGroup,
    string ChannelGroup,
    string Label,
    string Audience,
    string AudienceGroup,
    string SubjectTemplate,
    string BodyTemplate,
    string? AvailableVariables,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record UpdateNotificationTemplateRequest(
    string Label,
    string Audience,
    string SubjectTemplate,
    string BodyTemplate,
    string? AvailableVariables,
    bool IsActive);

public sealed record CreateNotificationTemplateRequest(
    string EventKey,
    string Channel,
    string Label,
    string Audience,
    string SubjectTemplate,
    string BodyTemplate,
    string? AvailableVariables,
    bool IsActive);
