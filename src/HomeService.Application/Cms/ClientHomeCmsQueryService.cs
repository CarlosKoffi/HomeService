using System.Text.Json;
using HomeService.Application.Abstractions;
using HomeService.Contracts.Cms;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Cms;

public sealed class ClientHomeCmsQueryService(IAppDbContext db)
{
    public async Task<ClientHomeCmsResponse?> GetAsync(
        string? language,
        string? country,
        CancellationToken cancellationToken)
    {
        var languageCode = string.IsNullOrWhiteSpace(language) ? "fr" : language.Trim().ToLowerInvariant();
        var countryCode = string.IsNullOrWhiteSpace(country) ? "CI" : country.Trim().ToUpperInvariant();

        var pageVersionId = await db.CmsPages
            .AsNoTracking()
            .Where(page => page.Site!.Code == "client-public")
            .Where(page => page.Code == "home")
            .Where(page => page.Site!.DefaultCountry == null || page.Site.DefaultCountry.IsoCode == countryCode)
            .Select(page => page.Versions
                .OrderByDescending(version => version.VersionNumber)
                .Select(version => (Guid?)version.Id)
                .FirstOrDefault())
            .FirstOrDefaultAsync(cancellationToken);

        if (pageVersionId is null)
        {
            return null;
        }

        var values = await db.CmsContentValues
            .AsNoTracking()
            .Where(value => value.Section!.PageVersionId == pageVersionId.Value)
            .Where(value => value.Language == null || value.Language.Code == languageCode)
            .Select(value => new CmsFieldProjection(
                value.Section!.ComponentDefinition!.Key,
                value.FieldKey,
                value.TextValue,
                value.JsonValue,
                value.MediaAssetId))
            .ToListAsync(cancellationToken);

        return BuildResponse(values);
    }

    private static ClientHomeCmsResponse BuildResponse(IReadOnlyList<CmsFieldProjection> values)
    {
        var hero = BuildFields(values, "HeroStandard");
        var services = BuildFields(values, "ServicesList");
        var steps = BuildFields(values, "StepsTimeline");
        var confidence = BuildFields(values, "TrustedLogos");
        var repeat = BuildFields(values, "FaqAccordion");
        var app = BuildFields(values, "DashboardPreview");
        var pathways = BuildFields(values, "ContactForm");
        var footer = BuildFields(values, "FooterLinks");

        return new ClientHomeCmsResponse(
            new CompanyHomeHeroCmsResponse(
                GetText(hero, "label", "Abidjan, Côte d’Ivoire"),
                GetText(hero, "headline", "Le bon service, au bon moment."),
                GetText(hero, "subtitle", "Des professionnels vérifiés pour votre maison, votre bien-être et votre quotidien."),
                new CmsLinkResponse(
                    GetText(hero, "primaryCta.label", "Commander un service"),
                    GetText(hero, "primaryCta.url", "#commander")),
                new CmsLinkResponse(
                    GetText(hero, "secondaryCta.label", "Découvrir Wélé"),
                    GetText(hero, "secondaryCta.url", "#services")),
                GetText(hero, "image.url", "website/client/wele-client-hero.png"),
                GetText(hero, "image.alt", "Une cliente accueille une professionnelle Wélé à Abidjan"),
                GetJsonList(hero, "proofItems", [
                    "Professionnels vérifiés et notés",
                    "Paiement sécurisé",
                    "Service suivi en direct",
                    "Assistance réactive"
                ])),
            new ClientHomeServicesCmsResponse(
                GetText(services, "label", "Des services pour chaque besoin"),
                GetText(services, "headline", "Tout ce que Wélé peut faire pour vous."),
                GetText(services, "subtitle", "Choisissez un univers, puis retrouvez les prestations réellement disponibles dans votre zone.")),
            new CompanyHomeStepsCmsResponse(
                GetText(steps, "label", "Simple, clair, suivi"),
                GetText(steps, "headline", "Commandez en toute confiance."),
                GetText(steps, "subtitle", "Une demande claire, un professionnel compatible et un suivi jusqu’à la fin."),
                GetJsonList(steps, "steps", [
                    new CmsStepResponse("01", "Service", "Choisissez votre service", "Décrivez votre besoin, ajoutez l’adresse et choisissez maintenant ou sur rendez-vous.", string.Empty),
                    new CmsStepResponse("02", "Professionnel", "Un professionnel vérifié accepte", "Nous cherchons l’entreprise et le professionnel compatibles avec votre demande.", string.Empty),
                    new CmsStepResponse("03", "Suivi", "Suivez, payez et évaluez", "Recevez les étapes importantes, payez au bon moment et partagez votre avis.", string.Empty)
                ])),
            new ClientHomeConfidenceCmsResponse(
                GetText(confidence, "label", "Pensé pour la confiance"),
                GetText(confidence, "headline", "Votre quotidien mérite des professionnels fiables."),
                GetText(confidence, "subtitle", "Chaque mission est encadrée : identité et compétences vérifiées, paiement au bon moment, suivi des étapes et avis après intervention."),
                new CmsLinkResponse(
                    GetText(confidence, "primaryCta.label", "Découvrir l’application"),
                    GetText(confidence, "primaryCta.url", "#telecharger")),
                GetText(confidence, "image.url", "website/client/wele-trust-team.png"),
                GetText(confidence, "image.alt", "Équipe de professionnels Wélé"),
                GetJsonList(confidence, "items", ["Identité contrôlée", "Compétences validées", "Avis authentiques"])),
            new ClientHomeRepeatCmsResponse(
                GetText(repeat, "label", "Votre historique devient utile"),
                GetText(repeat, "headline", "Un service vous a plu ?"),
                GetText(repeat, "subtitle", "Retrouvez votre entreprise favorite et relancez une demande en quelques secondes. Si elle est indisponible, Wélé continue la recherche pour vous."),
                new CmsLinkResponse(
                    GetText(repeat, "primaryCta.label", "Commander à nouveau"),
                    GetText(repeat, "primaryCta.url", "#telecharger")),
                GetText(repeat, "profile.name", "Awa Kouamé"),
                GetText(repeat, "profile.service", "Ménage & repassage"),
                GetText(repeat, "profile.rating", "4,9 ★"),
                GetText(repeat, "profile.badge", "Vérifiée"),
                GetText(repeat, "profile.image.url", "images/awa-kouame-profile.webp"),
                GetText(repeat, "profile.image.alt", "Portrait d’Awa Kouamé")),
            new ClientHomeAppCmsResponse(
                GetText(app, "label", "Tout Wélé dans votre téléphone"),
                GetText(app, "headline", "Wélé vous accompagne partout."),
                GetText(app, "subtitle", "Finalisez votre demande, recevez les notifications importantes, suivez l’arrivée du professionnel et retrouvez vos factures."),
                GetJsonList(app, "items", ["Suivi de mission", "Notifications utiles", "Paiement sécurisé", "Messagerie encadrée"])),
            new ClientHomePathwaysCmsResponse(
                GetText(pathways, "provider.headline", "Vous êtes professionnel ?"),
                GetText(pathways, "provider.subtitle", "Recevez des missions adaptées à vos compétences."),
                new CmsLinkResponse(
                    GetText(pathways, "providerCta.label", "Devenir prestataire"),
                    GetText(pathways, "providerCta.url", "https://pro.wele.africa")),
                GetText(pathways, "company.headline", "Vous êtes une entreprise ?"),
                GetText(pathways, "company.subtitle", "Développez et pilotez votre activité avec Wélé."),
                new CmsLinkResponse(
                    GetText(pathways, "companyCta.label", "Devenir partenaire"),
                    GetText(pathways, "companyCta.url", "https://entreprise.wele.africa"))),
            new CompanyHomeFooterCmsResponse(
                GetText(footer, "brandText", "Le bon service, au bon moment."),
                GetText(footer, "copyright", "© 2026 Wélé. Tous droits réservés."),
                GetText(footer, "baseline", "Abidjan · Côte d’Ivoire"),
                GetJsonList(footer, "columns", [
                    new CmsFooterColumnResponse("Services", ["Maison", "Bien-être", "Dépannage"]),
                    new CmsFooterColumnResponse("Rejoindre Wélé", ["Prestataires", "Entreprises"]),
                    new CmsFooterColumnResponse("Assistance", ["contact@wele.africa", "Confiance et sécurité"])
                ])));
    }

    private static Dictionary<string, string?> BuildFields(IReadOnlyList<CmsFieldProjection> values, string componentKey)
    {
        return values
            .Where(value => value.ComponentKey == componentKey)
            .GroupBy(value => value.FieldKey)
            .ToDictionary(
                group => group.Key,
                group => group.Select(value => value.MediaAssetId is null
                    ? value.JsonValue ?? value.TextValue
                    : $"/api/cms/media/{value.MediaAssetId}").FirstOrDefault(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static string GetText(IReadOnlyDictionary<string, string?> fields, string key, string fallback)
    {
        return fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }

    private static IReadOnlyList<T> GetJsonList<T>(IReadOnlyDictionary<string, string?> fields, string key, IReadOnlyList<T> fallback)
    {
        if (!fields.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        try
        {
            var result = JsonSerializer.Deserialize<IReadOnlyList<T>>(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
            return result.Count == 0 ? fallback : result;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private sealed record CmsFieldProjection(
        string ComponentKey,
        string FieldKey,
        string? TextValue,
        string? JsonValue,
        Guid? MediaAssetId);
}
