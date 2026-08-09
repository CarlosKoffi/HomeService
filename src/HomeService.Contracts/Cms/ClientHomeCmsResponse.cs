namespace HomeService.Contracts.Cms;

public sealed record ClientHomeCmsResponse(
    CompanyHomeHeroCmsResponse Hero,
    ClientHomeServicesCmsResponse Services,
    CompanyHomeStepsCmsResponse Steps,
    ClientHomeConfidenceCmsResponse Confidence,
    ClientHomeRepeatCmsResponse Repeat,
    ClientHomeAppCmsResponse App,
    ClientHomePathwaysCmsResponse Pathways,
    CompanyHomeFooterCmsResponse Footer);

public sealed record ClientHomeServicesCmsResponse(
    string Label,
    string Headline,
    string Subtitle);

public sealed record ClientHomeConfidenceCmsResponse(
    string Label,
    string Headline,
    string Subtitle,
    CmsLinkResponse PrimaryCta,
    string ImageUrl,
    string ImageAlt,
    IReadOnlyList<string> Benefits);

public sealed record ClientHomeRepeatCmsResponse(
    string Label,
    string Headline,
    string Subtitle,
    CmsLinkResponse PrimaryCta,
    string ProfileName,
    string ProfileService,
    string ProfileRating,
    string ProfileBadge,
    string ProfileImageUrl,
    string ProfileImageAlt);

public sealed record ClientHomeAppCmsResponse(
    string Label,
    string Headline,
    string Subtitle,
    IReadOnlyList<string> Benefits);

public sealed record ClientHomePathwaysCmsResponse(
    string ProviderHeadline,
    string ProviderSubtitle,
    CmsLinkResponse ProviderCta,
    string CompanyHeadline,
    string CompanySubtitle,
    CmsLinkResponse CompanyCta);

public static class ClientHomeCmsDefaults
{
    public static ClientHomeCmsResponse Create() => new(
        new CompanyHomeHeroCmsResponse(
            "Abidjan, Côte d’Ivoire",
            "Le bon service, au bon moment.",
            "Des professionnels vérifiés pour votre maison, votre bien-être et votre quotidien.",
            new CmsLinkResponse("Commander un service", "#commander"),
            new CmsLinkResponse("Découvrir Wélé", "#services"),
            "website/client/wele-client-hero.png",
            "Une cliente accueille une professionnelle Wélé à Abidjan",
            ["Professionnels vérifiés et notés", "Paiement sécurisé", "Service suivi en direct", "Assistance réactive"]),
        new ClientHomeServicesCmsResponse(
            "Des services pour chaque besoin",
            "Tout ce que Wélé peut faire pour vous.",
            "Choisissez un univers, puis retrouvez les prestations réellement disponibles dans votre zone."),
        new CompanyHomeStepsCmsResponse(
            "Simple, clair, suivi",
            "Commandez en toute confiance.",
            "Une demande claire, un professionnel compatible et un suivi jusqu’à la fin.",
            [
                new CmsStepResponse("01", "Service", "Choisissez votre service", "Décrivez votre besoin, ajoutez l’adresse et choisissez maintenant ou sur rendez-vous.", string.Empty),
                new CmsStepResponse("02", "Professionnel", "Un professionnel vérifié accepte", "Nous cherchons l’entreprise et le professionnel compatibles avec votre demande.", string.Empty),
                new CmsStepResponse("03", "Suivi", "Suivez, payez et évaluez", "Recevez les étapes importantes, payez au bon moment et partagez votre avis.", string.Empty)
            ]),
        new ClientHomeConfidenceCmsResponse(
            "Pensé pour la confiance",
            "Votre quotidien mérite des professionnels fiables.",
            "Chaque mission est encadrée : identité et compétences vérifiées, paiement au bon moment, suivi des étapes et avis après intervention.",
            new CmsLinkResponse("Découvrir l’application", "#telecharger"),
            "website/client/wele-trust-team.png",
            "Équipe de professionnels Wélé",
            ["Identité contrôlée", "Compétences validées", "Avis authentiques"]),
        new ClientHomeRepeatCmsResponse(
            "Votre historique devient utile",
            "Un service vous a plu ?",
            "Retrouvez votre entreprise favorite et relancez une demande en quelques secondes. Si elle est indisponible, Wélé continue la recherche pour vous.",
            new CmsLinkResponse("Commander à nouveau", "#telecharger"),
            "Awa Kouamé",
            "Ménage & repassage",
            "4,9 ★",
            "Vérifiée",
            "images/awa-kouame-profile.webp",
            "Portrait d’Awa Kouamé"),
        new ClientHomeAppCmsResponse(
            "Tout Wélé dans votre téléphone",
            "Wélé vous accompagne partout.",
            "Finalisez votre demande, recevez les notifications importantes, suivez l’arrivée du professionnel et retrouvez vos factures.",
            ["Suivi de mission", "Notifications utiles", "Paiement sécurisé", "Messagerie encadrée"]),
        new ClientHomePathwaysCmsResponse(
            "Vous êtes professionnel ?",
            "Recevez des missions adaptées à vos compétences.",
            new CmsLinkResponse("Devenir prestataire", "https://pro.wele.africa"),
            "Vous êtes une entreprise ?",
            "Développez et pilotez votre activité avec Wélé.",
            new CmsLinkResponse("Devenir partenaire", "https://entreprise.wele.africa")),
        new CompanyHomeFooterCmsResponse(
            "Le bon service, au bon moment.",
            "© 2026 Wélé. Tous droits réservés.",
            "Abidjan · Côte d’Ivoire",
            [
                new CmsFooterColumnResponse("Services", ["Maison", "Bien-être", "Dépannage"]),
                new CmsFooterColumnResponse("Rejoindre Wélé", ["Prestataires", "Entreprises"]),
                new CmsFooterColumnResponse("Assistance", ["contact@wele.africa", "Confiance et sécurité"])
            ]));
}
