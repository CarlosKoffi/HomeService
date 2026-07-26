using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class NotificationDeliveryRule : AuditableEntity
{
    private NotificationDeliveryRule()
    {
    }

    public NotificationDeliveryRule(
        string eventKey,
        string label,
        string audience,
        bool portalEnabled,
        bool mobileAppEnabled,
        bool emailEnabled,
        bool whatsAppEnabled,
        string? subjectTemplate = null,
        string? bodyTemplate = null)
    {
        EventKey = NormalizeRequired(eventKey, nameof(eventKey));
        Label = NormalizeRequired(label, nameof(label));
        Audience = NormalizeRequired(audience, nameof(audience));
        PortalEnabled = portalEnabled;
        MobileAppEnabled = mobileAppEnabled;
        EmailEnabled = emailEnabled;
        WhatsAppEnabled = whatsAppEnabled;
        SubjectTemplate = NormalizeTemplate(subjectTemplate, 180);
        BodyTemplate = NormalizeTemplate(bodyTemplate, 2000);
    }

    public string EventKey { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public string Audience { get; private set; } = string.Empty;
    public bool PortalEnabled { get; private set; }
    public bool MobileAppEnabled { get; private set; }
    public bool EmailEnabled { get; private set; }
    public bool WhatsAppEnabled { get; private set; }
    public string? SubjectTemplate { get; private set; }
    public string? BodyTemplate { get; private set; }

    public void Update(
        string label,
        string audience,
        bool portalEnabled,
        bool mobileAppEnabled,
        bool emailEnabled,
        bool whatsAppEnabled,
        string? subjectTemplate = null,
        string? bodyTemplate = null)
    {
        Label = NormalizeRequired(label, nameof(label));
        Audience = NormalizeRequired(audience, nameof(audience));
        PortalEnabled = portalEnabled;
        MobileAppEnabled = mobileAppEnabled;
        EmailEnabled = emailEnabled;
        WhatsAppEnabled = whatsAppEnabled;
        SubjectTemplate = NormalizeTemplate(subjectTemplate, 180);
        BodyTemplate = NormalizeTemplate(bodyTemplate, 2000);
        Touch();
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("La valeur est obligatoire.", parameterName);
        }

        return value.Trim();
    }

    private static string? NormalizeTemplate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }
}
