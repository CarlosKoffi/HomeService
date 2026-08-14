using HomeService.Contracts.Services;

namespace HomeService.Client.Services;

/// <summary>
/// Keeps the public website visual language consistent:
/// services use catalogue illustrations, while prestations use their own photos.
/// </summary>
public static class ServiceVisualCatalog
{
    public static string ServiceIllustration(ServiceSummaryResponse service)
    {
        if (!string.IsNullOrWhiteSpace(service.IconUrl))
        {
            return service.IconUrl;
        }

        if (IsServiceIllustration(service.ImageUrl))
        {
            return service.ImageUrl!;
        }

        return $"/assets/services/{IllustrationFileName(ServiceSeoCatalog.ToSlug(service.Name))}";
    }

    public static string? PrestationPhoto(ServicePrestationSummaryResponse prestation)
        => string.IsNullOrWhiteSpace(prestation.IllustrationUrl)
            ? null
            : prestation.IllustrationUrl;

    private static bool IsServiceIllustration(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value.Replace('\\', '/').Contains("/assets/services/", StringComparison.OrdinalIgnoreCase);

    private static string IllustrationFileName(string slug) => slug switch
    {
        "menage" or "menage-a-domicile" or "nettoyage" => "menage.png",
        "blanchisserie" or "pressing" or "repassage" => "blanchisserie.png",
        "depannage-auto" or "assistance-auto" => "depannage-auto.png",
        "nounou" or "garde-enfants" or "garde-d-enfant" => "nounou.png",
        "manucure-et-pedicure" => "manucure-pedicure.png",
        "massage-et-bien-etre" => "massage-bien-etre.png",
        "maquillage-professionnel" => "maquillage-professionnel.png",
        "anti-nuisibles" => "anti-nuisibles.png",
        "electromenager" => "electromenager.png",
        "electricite" => "electricite.png",
        "climatisation" => "climatisation.png",
        "plomberie" => "plomberie.png",
        "serrurerie" => "serrurerie.png",
        "jardinage" => "jardinage.png",
        "peinture" => "peinture.png",
        "coiffure" => "coiffure.png",
        "estheticienne" => "estheticienne.png",
        "barbier" => "barbier.png",
        _ => "service-generique.png"
    };
}
