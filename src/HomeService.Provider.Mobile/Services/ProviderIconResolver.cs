using System.Globalization;
using System.Text;

namespace HomeService.Provider.Mobile.Services;

public static class ProviderIconResolver
{
    public static string ForService(string? iconName, string? serviceName = null)
    {
        var key = Normalize($"{iconName} {serviceName}");
        if (ContainsAny(key, "faucet", "plumb", "evier", "robinet", "sanitaire")) return "service_plumbing.svg";
        if (ContainsAny(key, "zap", "electric", "disjoncteur", "luminaire")) return "service_electric.svg";
        if (ContainsAny(key, "sparkles", "clean", "menage", "nettoyage")) return "service_cleaning.svg";
        if (ContainsAny(key, "sprout", "garden", "jardin")) return "service_garden.svg";
        if (ContainsAny(key, "shirt", "laundry", "blanch", "pressing", "repass")) return "service_laundry.svg";
        if (ContainsAny(key, "car", "auto", "vehicule", "remorqu")) return "service_auto.svg";
        if (ContainsAny(key, "baby", "nounou", "enfant", "garde")) return "service_child.svg";
        if (ContainsAny(key, "beauty", "esthet", "coiff", "barbier", "massage", "manucure", "maquillage")) return "service_beauty.svg";
        return "service_default.svg";
    }

    public static string ForNotification(string? relatedEntityType, string? title)
    {
        var key = Normalize($"{relatedEntityType} {title}");
        if (ContainsAny(key, "message", "conversation")) return "icon_message.svg";
        if (ContainsAny(key, "mission", "intervention", "affect", "arrive", "prestation")) return "icon_mission.svg";
        if (ContainsAny(key, "profil", "profile", "document")) return "icon_user.svg";
        return "icon_info.svg";
    }

    private static bool ContainsAny(string value, params string[] candidates)
        => candidates.Any(value.Contains);

    private static string Normalize(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
