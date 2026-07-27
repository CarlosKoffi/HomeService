using HomeService.Domain.Enums;

namespace HomeService.Application.Admin;

internal static class NotificationAdminGrouping
{
    public static string AudienceGroup(string audience)
    {
        return Normalize(audience) switch
        {
            "Company" => "Entreprise",
            "Provider" => "Prestataire",
            "Customer" => "Client",
            "Mixed" => "Global",
            _ => "Autres"
        };
    }

    public static string ChannelGroup(NotificationTemplateChannel channel)
    {
        return channel switch
        {
            NotificationTemplateChannel.Portal => "Portail",
            NotificationTemplateChannel.MobilePush => "Application mobile",
            NotificationTemplateChannel.Email => "Email",
            NotificationTemplateChannel.WhatsApp => "WhatsApp",
            _ => "Autres"
        };
    }

    public static string EventGroup(string eventKey)
    {
        var key = Normalize(eventKey);
        if (key.Contains("Dispute", StringComparison.OrdinalIgnoreCase)
            || key.Contains("Refund", StringComparison.OrdinalIgnoreCase)
            || key.Contains("Cancellation", StringComparison.OrdinalIgnoreCase)
            || key.Contains("Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            return "Litiges et annulations";
        }

        if (key.Contains("Payment", StringComparison.OrdinalIgnoreCase)
            || key.Contains("Payout", StringComparison.OrdinalIgnoreCase)
            || key.Contains("Commission", StringComparison.OrdinalIgnoreCase))
        {
            return "Paiements";
        }

        if (key.Contains("CompanyApplication", StringComparison.OrdinalIgnoreCase)
            || key.Contains("CompanyDocument", StringComparison.OrdinalIgnoreCase)
            || key.Contains("CompanyActivation", StringComparison.OrdinalIgnoreCase))
        {
            return "Dossiers entreprise";
        }

        if (key.Contains("Interim", StringComparison.OrdinalIgnoreCase)
            || key.Contains("ProviderProfile", StringComparison.OrdinalIgnoreCase))
        {
            return "Prestataires";
        }

        if (key.Contains("Mission", StringComparison.OrdinalIgnoreCase))
        {
            return "Missions";
        }

        return "Autres";
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
