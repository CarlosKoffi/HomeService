namespace HomeService.Contracts.Notifications;

public sealed record AdminCompanyPortalNotificationResponse(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    string Type,
    string Title,
    string Message,
    string Tone,
    bool IsRead,
    string? ActionUrl,
    DateTimeOffset OccurredAt);
