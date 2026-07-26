using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class NotificationTemplate : AuditableEntity
{
    private NotificationTemplate()
    {
    }

    public NotificationTemplate(
        string eventKey,
        NotificationTemplateChannel channel,
        string label,
        string audience,
        string subjectTemplate,
        string bodyTemplate,
        string? availableVariables)
    {
        EventKey = CleanRequired(eventKey, 96);
        Channel = channel;
        Label = CleanRequired(label, 180);
        Audience = CleanRequired(audience, 32);
        SubjectTemplate = CleanRequired(subjectTemplate, 180);
        BodyTemplate = CleanRequired(bodyTemplate, 2000);
        AvailableVariables = Clean(availableVariables, 1000);
        IsActive = true;
    }

    public string EventKey { get; private set; } = string.Empty;
    public NotificationTemplateChannel Channel { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public string Audience { get; private set; } = string.Empty;
    public string SubjectTemplate { get; private set; } = string.Empty;
    public string BodyTemplate { get; private set; } = string.Empty;
    public string? AvailableVariables { get; private set; }
    public bool IsActive { get; private set; }
    public NotificationDeliveryRule? DeliveryRule { get; private set; }

    public void Update(string label, string audience, string subjectTemplate, string bodyTemplate, string? availableVariables, bool isActive)
    {
        Label = CleanRequired(label, 180);
        Audience = CleanRequired(audience, 32);
        SubjectTemplate = CleanRequired(subjectTemplate, 180);
        BodyTemplate = CleanRequired(bodyTemplate, 2000);
        AvailableVariables = Clean(availableVariables, 1000);
        IsActive = isActive;
        Touch();
    }

    private static string CleanRequired(string value, int maxLength)
    {
        var cleaned = Clean(value, maxLength);
        if (cleaned is null)
        {
            throw new ArgumentException("La valeur est obligatoire.", nameof(value));
        }

        return cleaned;
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }
}
