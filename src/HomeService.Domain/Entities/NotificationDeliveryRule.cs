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
        bool whatsAppEnabled)
    {
        EventKey = NormalizeRequired(eventKey, nameof(eventKey));
        Label = NormalizeRequired(label, nameof(label));
        Audience = NormalizeRequired(audience, nameof(audience));
        PortalEnabled = portalEnabled;
        MobileAppEnabled = mobileAppEnabled;
        EmailEnabled = emailEnabled;
        WhatsAppEnabled = whatsAppEnabled;
    }

    public string EventKey { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public string Audience { get; private set; } = string.Empty;
    public bool PortalEnabled { get; private set; }
    public bool MobileAppEnabled { get; private set; }
    public bool EmailEnabled { get; private set; }
    public bool WhatsAppEnabled { get; private set; }

    public void Update(
        string label,
        string audience,
        bool portalEnabled,
        bool mobileAppEnabled,
        bool emailEnabled,
        bool whatsAppEnabled)
    {
        Label = NormalizeRequired(label, nameof(label));
        Audience = NormalizeRequired(audience, nameof(audience));
        PortalEnabled = portalEnabled;
        MobileAppEnabled = mobileAppEnabled;
        EmailEnabled = emailEnabled;
        WhatsAppEnabled = whatsAppEnabled;
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
}
