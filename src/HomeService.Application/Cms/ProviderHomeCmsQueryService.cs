using System.Text.Json;
using HomeService.Application.Abstractions;
using HomeService.Contracts.Cms;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Cms;

public sealed class ProviderHomeCmsQueryService(IAppDbContext db)
{
    public async Task<CompanyHomeCmsResponse?> GetAsync(
        string? language,
        string? country,
        CancellationToken cancellationToken)
    {
        var languageCode = string.IsNullOrWhiteSpace(language) ? "fr" : language.Trim().ToLowerInvariant();
        var countryCode = string.IsNullOrWhiteSpace(country) ? "CI" : country.Trim().ToUpperInvariant();

        var pageVersionId = await db.CmsPages
            .AsNoTracking()
            .Where(page => page.Site!.Code == "provider-public")
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

        return BuildProviderHomeCmsResponse(values);
    }

    private static CompanyHomeCmsResponse BuildProviderHomeCmsResponse(IReadOnlyList<CmsFieldProjection> values)
    {
        var hero = BuildFields(values, "HeroStandard");
        var steps = BuildFields(values, "StepsTimeline");
        var trusted = BuildFields(values, "TrustedLogos");
        var dashboard = BuildFields(values, "DashboardPreview");
        var faq = BuildFields(values, "FaqAccordion");
        var contact = BuildFields(values, "ContactForm");
        var footer = BuildFields(values, "FooterLinks");

        return new CompanyHomeCmsResponse(
            new CompanyHomeHeroCmsResponse(
                GetText(hero, "label", "wélé prestataire"),
                GetText(hero, "headline", "Trouvez des missions. Travaillez avec des entreprises verifiees."),
                GetText(hero, "subtitle", "Creez votre profil, rattachez-vous a une entreprise partenaire ou proposez votre candidature en interim."),
                new CmsLinkResponse(
                    GetText(hero, "primaryCta.label", "Creer un compte"),
                    GetText(hero, "primaryCta.url", "/onboarding?mode=register")),
                new CmsLinkResponse(
                    GetText(hero, "secondaryCta.label", "Voir le fonctionnement"),
                    GetText(hero, "secondaryCta.url", "#how")),
                GetText(hero, "image.url", "images/kaza-provider-hero.png"),
                GetText(hero, "image.alt", "Prestataires de services a domicile wélé"),
                GetJsonList(hero, "proofItems", ["Code entreprise", "Profil interim", "Missions proches"])),
            new CompanyHomeStepsCmsResponse(
                GetText(steps, "label", "Fonctionnement"),
                GetText(steps, "headline", "Trois etapes pour demarrer."),
                GetText(steps, "subtitle", "Un parcours simple pour proposer votre profil en interim a une entreprise partenaire."),
                GetJsonList(steps, "steps", [
                    new CmsStepResponse("01", "Formulaire", "Creez votre compte en ligne", "Renseignez vos informations, votre service principal et votre zone.", "images/kaza-provider-step-1.svg"),
                    new CmsStepResponse("02", "Entreprise", "Choisissez une entreprise", "wélé vous propose des entreprises qui acceptent les profils interimaires dans votre domaine.", "images/kaza-provider-step-2.svg"),
                    new CmsStepResponse("03", "Validation", "L'entreprise etudie votre demande", "Si elle vous valide, vous pourrez recevoir des missions dans l'application mobile.", "images/kaza-provider-step-3.svg")
                ])),
            new CompanyHomeTrustedCmsResponse(
                GetText(trusted, "headline", "Des opportunites simples et suivies"),
                GetJsonList(trusted, "items", ["Entreprises partenaires", "Missions proches", "Profil mobile", "Suivi des interventions", "Chat client apres acceptation"])),
            new CompanyHomeDashboardCmsResponse(
                GetText(dashboard, "label", "Application"),
                GetText(dashboard, "headline", "Tout tient dans votre telephone."),
                GetText(dashboard, "subtitle", "Vos missions, vos services, vos messages et votre profil restent clairs, meme avec peu de connexion."),
                GetJsonList(dashboard, "stats", [
                    new CmsDashboardStatResponse("Mission", "1", "A traiter a la fois"),
                    new CmsDashboardStatResponse("Distance", "2 km", "Zone proche"),
                    new CmsDashboardStatResponse("Profil", "92%", "Presque complet")
                ]),
                GetJsonList(dashboard, "requests", ["Mission menage a Cocody", "Demande jardinage a Marcory", "Rendez-vous electricite demain"]),
                GetJsonList(dashboard, "providers", ["Disponible maintenant", "Code entreprise actif", "Book photo a completer"])),
            new CompanyHomeFaqCmsResponse(
                GetText(faq, "label", "FAQ"),
                GetText(faq, "headline", "Foire aux questions"),
                GetJsonList(faq, "questions", [
                    new CmsFaqItemResponse("Je peux m'inscrire sans entreprise ?", "Oui. Vous creez un profil interim. Une entreprise devra ensuite vous valider avant les missions."),
                    new CmsFaqItemResponse("A quoi sert le code entreprise ?", "Il permet d'activer le profil que votre entreprise a deja cree pour vous."),
                    new CmsFaqItemResponse("Quand vois-je le numero du client ?", "Apres acceptation et confirmation de la mission, les contacts utiles deviennent visibles."),
                    new CmsFaqItemResponse("Pourquoi ajouter des photos ?", "Pour certains services, un book aide l'entreprise a valider votre profil et vos prestations.")
                ])),
            new CompanyHomeContactCmsResponse(
                GetText(contact, "label", "Contact"),
                GetText(contact, "headline", "Besoin d'aide pour demarrer ?"),
                GetText(contact, "subtitle", "Laissez vos coordonnees. Nous vous orientons vers le bon parcours: code entreprise ou profil interim."),
                GetJsonList(contact, "tags", ["Abidjan", "Interim", "Services a domicile"])),
            new CompanyHomeFooterCmsResponse(
                GetText(footer, "brandText", "La plateforme qui rapproche les prestataires serieux des entreprises de services."),
                GetText(footer, "copyright", "(c) 2026 wélé Technologies. Tous droits reserves."),
                GetText(footer, "baseline", "Concu pour l'Afrique de l'Ouest"),
                GetJsonList(footer, "columns", [
                    new CmsFooterColumnResponse("Produit", ["Application", "Fonctionnement", "Securite", "Support"]),
                    new CmsFooterColumnResponse("Prestataire", ["Creer un profil", "Activer un code", "Missions", "Profil interim"]),
                    new CmsFooterColumnResponse("Ressources", ["Centre d'aide", "FAQ", "Contact", "WhatsApp"]),
                    new CmsFooterColumnResponse("Legal", ["CGU", "Confidentialite", "Mentions legales"])
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
