using HomeService.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Notifications;

public sealed class NotificationDeliveryPreferenceService(IAppDbContext db)
{
    public async Task<NotificationDeliveryPreference> GetAsync(
        string eventKey,
        string audience,
        bool defaultEmailEnabled,
        bool defaultWhatsAppEnabled,
        CancellationToken cancellationToken)
    {
        var normalizedAudience = NormalizeAudience(audience);
        var rule = await db.NotificationDeliveryRules
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.EventKey == eventKey, cancellationToken);

        var emailEnabled = rule?.EmailEnabled ?? defaultEmailEnabled;
        var whatsAppEnabled = rule?.WhatsAppEnabled ?? defaultWhatsAppEnabled;

        return new NotificationDeliveryPreference(
            IsPortalAutomatic(normalizedAudience),
            IsMobileAppAutomatic(normalizedAudience),
            emailEnabled,
            whatsAppEnabled);
    }

    public static bool IsPortalAutomatic(string audience)
    {
        var normalizedAudience = NormalizeAudience(audience);
        return normalizedAudience is "Company" or "Mixed";
    }

    public static bool IsMobileAppAutomatic(string audience)
    {
        var normalizedAudience = NormalizeAudience(audience);
        return normalizedAudience is "Provider" or "Customer" or "Mixed";
    }

    private static string NormalizeAudience(string audience)
    {
        return string.IsNullOrWhiteSpace(audience) ? "Mixed" : audience.Trim();
    }
}

public sealed record NotificationDeliveryPreference(
    bool PortalEnabled,
    bool MobileAppEnabled,
    bool EmailEnabled,
    bool WhatsAppEnabled);
