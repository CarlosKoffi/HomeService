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
                value.JsonValue,
                value.MediaAssetId))
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
                GetText(hero, "headline", "La plateforme qui fait grandir votre entreprise de services"),
                GetText(hero, "subtitle", "wélé connecte votre entreprise à des clients et vous donne les outils pour gérer vos techniciens, vos missions et vos revenus. Le tout depuis une interface unique."),
                new CmsLinkResponse(
                    GetText(hero, "primaryCta.label", "Devenir partenaire"),
                    GetText(hero, "primaryCta.url", "register")),
                new CmsLinkResponse(
                    GetText(hero, "secondaryCta.label", "Voir le fonctionnement"),
                    GetText(hero, "secondaryCta.url", "#how")),
                GetText(hero, "image.url", "images/wele-premium-hero.png"),
                GetText(hero, "image.alt", "Equipe wélé en intervention chez un client"),
                GetJsonList(hero, "proofItems", ["Inscription gratuite", "Validation sous 48h", "Support partenaire 24/7"])),
            new CompanyHomeStepsCmsResponse(
                GetText(steps, "label", "Comment ca marche"),
                GetText(steps, "headline", "De l'inscription à votre première mission"),
                GetText(steps, "subtitle", "Un parcours clair en trois étapes."),
                GetJsonList(steps, "steps", [
                    new CmsStepResponse("01", "Compte", "Créez votre compte entreprise", "Renseignez les informations et les pièces légales et administratives de votre entreprise.", "images/wele-how-step-1.png"),
                    new CmsStepResponse("02", "Validation", "Validation par nos équipes", "Nous vérifions et approuvons votre dossier sous 48h.", "images/wele-how-step-2.png"),
                    new CmsStepResponse("03", "Demandes", "Recevez des demandes", "Ajoutez et gérez vos techniciens, recevez des demandes et suivez vos interventions.", "images/wele-how-step-3.png")
                ])),
            new CompanyHomeTrustedCmsResponse(
                GetText(trusted, "headline", "Tout ce qu'il faut pour développer votre activité"),
                GetText(trusted, "subtitle", "Une infrastructure professionnelle conçue pour vous apporter des clients et simplifier votre gestion."),
                GetJsonList(trusted, "items", ["Demandes qualifiées", "Gestion des techniciens", "Suivi des missions", "Paiements sécurisés", "Visibilité locale", "Support partenaire 24/7"])),
            new CompanyHomeDashboardCmsResponse(
                GetText(dashboard, "label", "Le tableau de bord"),
                GetText(dashboard, "headline", "Une interface unique pour piloter votre entreprise"),
                GetText(dashboard, "subtitle", "Demandes, équipes, missions et paiements : tout est réuni au même endroit."),
                GetJsonList(dashboard, "stats", [
                    new CmsDashboardStatResponse("Demandes", "12", "+4 cette semaine"),
                    new CmsDashboardStatResponse("Assignées", "8", "Equipe mobilisée"),
                    new CmsDashboardStatResponse("Paiements", "185k", "XOF suivis")
                ]),
                GetJsonList(dashboard, "requests", ["Ménage à Cocody Riviera", "Jardinage à Marcory", "Nounou aux Deux Plateaux"]),
                GetJsonList(dashboard, "providers", ["Awa K. - Ménage", "Jean M. - Jardinage", "Fatou C. - Nounou"])),
            new CompanyHomeFaqCmsResponse(
                GetText(faq, "label", "FAQ"),
                GetText(faq, "headline", "Foire aux questions"),
                GetJsonList(faq, "questions", [
                    new CmsFaqItemResponse("Quel type de sociétés peuvent s'inscrire sur wélé ?", "Les entreprises de jardinage, électricité, ménage à domicile, blanchisserie, dépannage auto, nounou, plomberie, climatisation, peinture, serrurerie, déménagement, maintenance maison et autres services de proximité peuvent rejoindre la plateforme selon le catalogue ouvert dans l'admin."),
                    new CmsFaqItemResponse("Comment sont vérifiées les entreprises sur wélé ?", "Nous vérifions les informations de l'entreprise, les documents essentiels et le contact responsable avant l'activation complète."),
                    new CmsFaqItemResponse("L'inscription est-elle gratuite ?", "Oui, la création de votre compte entreprise est entièrement gratuite. wélé ne prélève qu'une commission sur les missions réalisées avec succès."),
                    new CmsFaqItemResponse("Puis-je refuser une demande ?", "Absolument. Vous restez libre d'accepter ou de refuser chaque demande selon votre disponibilité et votre zone d'intervention."),
                    new CmsFaqItemResponse("Qui choisit le technicien ?", "C'est vous. Vous affectez le technicien de votre équipe que vous jugez le plus adapté à chaque intervention, ou vous laissez wélé s'en occuper."),
                    new CmsFaqItemResponse("Comment sont suivis les paiements ?", "Les paiements sont sécurisés et versés rapidement sur votre compte après la clôture de chaque mission."),
                    new CmsFaqItemResponse("Qui gère les réclamations clients ?", "wélé gère le support client de premier niveau, en coordination avec votre entreprise lorsque cela est nécessaire."),
                    new CmsFaqItemResponse("Puis-je mettre mon compte en pause ?", "Oui, vous pouvez suspendre temporairement votre activité à tout moment depuis vos paramètres."),
                    new CmsFaqItemResponse("Combien de temps prend la validation ?", "La validation de votre dossier est généralement réalisée sous 48h après réception de vos documents.")
                ])),
            new CompanyHomeContactCmsResponse(
                GetText(contact, "label", "Contact"),
                GetText(contact, "headline", "Vous voulez en parler avant de vous inscrire ?"),
                GetText(contact, "subtitle", "Laissez vos coordonnées. Nous vous rappelons pour voir comment wélé peut aider votre entreprise."),
                GetJsonList(contact, "tags", ["Abidjan", "Services à domicile", "Partenariat entreprise"])),
            new CompanyHomeFooterCmsResponse(
                GetText(footer, "brandText", "La plateforme B2B pour connecter clients, entreprises et professionnels de confiance."),
                GetText(footer, "copyright", "(c) 2026 wélé Technologies. Tous droits réservés."),
                GetText(footer, "baseline", "Conçu pour l'Afrique de l'Ouest"),
                GetJsonList(footer, "columns", [
                    new CmsFooterColumnResponse("Produit", ["Plateforme", "Fonctionnement", "Tarifs", "Sécurité"]),
                    new CmsFooterColumnResponse("Entreprise", ["A propos", "Blog", "Carrières", "Partenaires"]),
                    new CmsFooterColumnResponse("Ressources", ["Documentation", "Centre d'aide", "Communauté"]),
                    new CmsFooterColumnResponse("Légal", ["CGU", "Confidentialité", "Mentions légales"])
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
