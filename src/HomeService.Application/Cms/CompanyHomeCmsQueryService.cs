using System.Text.Json;
using HomeService.Application.Abstractions;
using HomeService.Contracts.Cms;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Cms;

public sealed class CompanyHomeCmsQueryService(IAppDbContext db)
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
            .Where(page => page.Site!.Code == "company-public")
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
                value.JsonValue))
            .ToListAsync(cancellationToken);

        return BuildCompanyHomeCmsResponse(values);
    }

    private static CompanyHomeCmsResponse BuildCompanyHomeCmsResponse(IReadOnlyList<CmsFieldProjection> values)
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
                GetText(hero, "label", "Plateforme partenaire"),
                GetText(hero, "headline", "Recevez plus de missions. Developpez votre entreprise."),
                GetText(hero, "subtitle", "Kaza connecte les clients aux entreprises de services a domicile verifiees."),
                new CmsLinkResponse(
                    GetText(hero, "primaryCta.label", "Commencer"),
                    GetText(hero, "primaryCta.url", "register")),
                new CmsLinkResponse(
                    GetText(hero, "secondaryCta.label", "Voir le fonctionnement"),
                    GetText(hero, "secondaryCta.url", "#how")),
                GetText(hero, "image.url", "images/kaza-premium-hero.png"),
                GetText(hero, "image.alt", "Equipe Kaza en intervention chez un client"),
                GetJsonList(hero, "proofItems", ["Inscription gratuite", "Validation dossier", "Portail entreprise"])),
            new CompanyHomeStepsCmsResponse(
                GetText(steps, "label", "Comment ca marche"),
                GetText(steps, "headline", "Trois etapes, puis votre portail est pret."),
                GetText(steps, "subtitle", "Un parcours court pour verifier l'entreprise et demarrer avec une base claire."),
                GetJsonList(steps, "steps", [
                    new CmsStepResponse("01", "Compte", "Creez votre compte", "Renseignez votre entreprise, vos services et le contact responsable.", "images/kaza-how-step-1.png"),
                    new CmsStepResponse("02", "Verification", "Nous verifions votre dossier", "Kaza controle les informations pour securiser les clients et les missions.", "images/kaza-how-step-2.png"),
                    new CmsStepResponse("03", "Portail", "Travaillez depuis votre portail", "Ajoutez vos prestataires, recevez des demandes et suivez vos interventions.", "images/kaza-how-step-3.png")
                ])),
            new CompanyHomeTrustedCmsResponse(
                GetText(trusted, "headline", "Ils font confiance a Kaza"),
                GetJsonList(trusted, "items", ["Services verifies", "Entreprises locales", "Prestataires suivis", "Paiements traces", "Support partenaire"])),
            new CompanyHomeDashboardCmsResponse(
                GetText(dashboard, "label", "Dashboard"),
                GetText(dashboard, "headline", "Tout ce qui compte, lisible en un coup d'oeil."),
                GetText(dashboard, "subtitle", "Demandes, equipe, missions, documents et paiements restent au meme endroit."),
                GetJsonList(dashboard, "stats", [
                    new CmsDashboardStatResponse("Demandes", "12", "+4 cette semaine"),
                    new CmsDashboardStatResponse("Assignees", "8", "Equipe mobilisee"),
                    new CmsDashboardStatResponse("Paiements", "185k", "XOF suivis")
                ]),
                GetJsonList(dashboard, "requests", ["Menage a Cocody Riviera", "Jardinage a Marcory", "Nounou aux Deux Plateaux"]),
                GetJsonList(dashboard, "providers", ["Awa K. - Menage", "Jean M. - Jardinage", "Fatou C. - Nounou"])),
            new CompanyHomeFaqCmsResponse(
                GetText(faq, "label", "FAQ"),
                GetText(faq, "headline", "Foire aux questions"),
                GetJsonList(faq, "questions", [
                    new CmsFaqItemResponse("Comment sont verifiees les entreprises sur Kaza ?", "Nous verifions les informations de l'entreprise, les documents essentiels et le contact responsable avant l'activation complete."),
                    new CmsFaqItemResponse("L'inscription est-elle gratuite ?", "Oui. L'inscription est gratuite. Kaza applique ensuite une commission uniquement sur les missions realisees."),
                    new CmsFaqItemResponse("Puis-je refuser une demande client ?", "Oui. Votre entreprise reste libre d'accepter les demandes qui correspondent a son equipe, sa zone et ses disponibilites.")
                ])),
            new CompanyHomeContactCmsResponse(
                GetText(contact, "label", "Contact"),
                GetText(contact, "headline", "Vous voulez en parler avant de vous inscrire ?"),
                GetText(contact, "subtitle", "Laissez vos coordonnees. Nous vous rappelons pour voir comment Kaza peut aider votre entreprise."),
                GetJsonList(contact, "tags", ["Abidjan", "Services a domicile", "Partenariat entreprise"])),
            new CompanyHomeFooterCmsResponse(
                GetText(footer, "brandText", "La plateforme B2B pour connecter clients, entreprises et professionnels de confiance."),
                GetText(footer, "copyright", "(c) 2026 Kaza Technologies. Tous droits reserves."),
                GetText(footer, "baseline", "Concu pour l'Afrique de l'Ouest"),
                GetJsonList(footer, "columns", [
                    new CmsFooterColumnResponse("Produit", ["Plateforme", "Fonctionnement", "Tarifs", "Securite"]),
                    new CmsFooterColumnResponse("Entreprise", ["A propos", "Blog", "Carrieres", "Partenaires"]),
                    new CmsFooterColumnResponse("Ressources", ["Documentation", "Centre d'aide", "Communaute"]),
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
                group => group.Select(value => value.JsonValue ?? value.TextValue).FirstOrDefault(),
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
        string? JsonValue);
}
