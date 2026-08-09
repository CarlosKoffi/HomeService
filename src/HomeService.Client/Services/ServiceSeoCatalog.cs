using System.Globalization;
using System.Text;
using HomeService.Contracts.Services;

namespace HomeService.Client.Services;

public static class ServiceSeoCatalog
{
    private static readonly string[] NavigationPriority =
    [
        "menage",
        "plomberie",
        "climatisation",
        "electricite",
        "blanchisserie",
        "coiffure",
        "estheticienne",
        "depannage-auto",
    ];

    private static readonly IReadOnlyDictionary<string, ServiceSeoProfile> Profiles =
        new Dictionary<string, ServiceSeoProfile>(StringComparer.Ordinal)
        {
            ["menage"] = Profile(
                "Ménage à domicile à Abidjan : toutes les prestations avec Wélé.",
                "Découvrez les prestations de ménage à domicile disponibles à Abidjan : entretien régulier, grand nettoyage, vitres, cuisine et sanitaires.",
                "Entretien du domicile, nettoyage ponctuel ou remise au propre : retrouvez les prestations de ménage actives et leurs options dans le catalogue Wélé.",
                "Bien préparer une intervention de ménage",
                "Une demande précise aide le professionnel à comprendre la surface, les pièces concernées et le niveau de nettoyage attendu.",
                Tips(
                    ("Décrire le logement", "Indiquez le type de logement et les pièces principalement concernées."),
                    ("Préciser la priorité", "Signalez les zones qui demandent le plus d’attention : cuisine, sanitaires, vitres ou sols."),
                    ("Choisir l’option adaptée", "Lorsque des options sont proposées, sélectionnez celle qui correspond à la taille réelle du logement."))),
            ["jardinage"] = Profile(
                "Jardinier à Abidjan : entretien de jardin avec Wélé.",
                "Trouvez les prestations de jardinage disponibles à Abidjan : tonte, taille de haie, désherbage, arrosage et entretien extérieur.",
                "Pelouse, haies, plantes ou terrasse extérieure : découvrez les prestations de jardinage proposées par les professionnels référencés sur Wélé.",
                "Préparer votre besoin de jardinage",
                "La surface et la nature de l’espace extérieur permettent de mieux orienter la demande vers la bonne prestation.",
                Tips(
                    ("Évaluer la surface", "Indiquez une estimation de la zone à entretenir lorsque vous la connaissez."),
                    ("Identifier les végétaux", "Précisez s’il s’agit de gazon, de haies, de plantes ou d’un entretien général."),
                    ("Signaler l’accès", "Mentionnez les contraintes d’accès ou d’évacuation utiles au professionnel."))),
            ["electricite"] = Profile(
                "Électricien à Abidjan : dépannage électrique avec Wélé.",
                "Découvrez les prestations d’électricité à Abidjan : diagnostic de panne, prise, interrupteur, luminaire, disjoncteur et court-circuit simple.",
                "Panne localisée, prise défectueuse, luminaire ou disjoncteur : consultez les interventions électriques couvertes par le catalogue Wélé.",
                "Décrire clairement une panne électrique",
                "Pour votre sécurité, ne manipulez pas une installation dégradée. Décrivez simplement les symptômes observés dans l’application.",
                Tips(
                    ("Repérer la zone", "Indiquez la pièce ou le circuit où le problème apparaît."),
                    ("Décrire le symptôme", "Précisez si la panne est permanente, intermittente ou liée à un équipement."),
                    ("Rester prudent", "En présence d’odeur, d’étincelles ou de surchauffe, coupez l’alimentation si cela peut être fait sans risque."))),
            ["plomberie"] = Profile(
                "Plombier à Abidjan : dépannage et interventions avec Wélé.",
                "Besoin d’un plombier à Abidjan ? Découvrez les prestations Wélé : fuite, évier ou WC bouché, robinet, sanitaire et chauffe-eau.",
                "Fuite d’eau, évier bouché, WC, robinet ou installation sanitaire : découvrez tous les besoins de plomberie pris en charge sur Wélé.",
                "Identifier votre besoin de plomberie",
                "Quelques informations simples permettent de distinguer un débouchage, une fuite accessible ou un équipement à remplacer.",
                Tips(
                    ("Localiser le problème", "Indiquez la pièce, l’équipement et la zone exacte où le problème est visible."),
                    ("Décrire l’urgence", "Précisez si l’eau coule en continu, si l’équipement reste utilisable ou si la zone est inaccessible."),
                    ("Ajouter des photos", "Des photos nettes aident à comprendre le besoin avant le déplacement."))),
            ["blanchisserie"] = Profile(
                "Blanchisserie à Abidjan : lavage, pliage et repassage avec Wélé.",
                "Découvrez la blanchisserie Wélé à Abidjan : lavage et pliage, repassage, linge de maison, pressing tenue et détachage simple.",
                "Linge du quotidien, repassage, draps, serviettes ou tenue à entretenir : retrouvez toutes les prestations et options de blanchisserie actives.",
                "Bien préparer votre linge",
                "Une préparation claire aide le professionnel à distinguer les lots et à identifier les besoins particuliers.",
                Tips(
                    ("Séparer les lots", "Regroupez le linge courant, le linge de maison et les tenues nécessitant un entretien particulier."),
                    ("Signaler les taches", "Indiquez les taches visibles afin que le besoin de détachage soit compris."),
                    ("Choisir la quantité", "Sélectionnez l’option correspondant au poids du linge ou au nombre de pièces."))),
            ["depannage-auto"] = Profile(
                "Dépannage auto à Abidjan : assistance de proximité avec Wélé.",
                "Découvrez les services de dépannage auto à Abidjan : batterie, crevaison, démarrage, diagnostic, carburant d’urgence et remorquage.",
                "Batterie, crevaison, panne de démarrage ou manque de carburant : consultez les prestations d’assistance auto disponibles dans votre zone.",
                "Transmettre les bonnes informations sur le véhicule",
                "La position du véhicule, son modèle et les symptômes observés permettent de mieux orienter l’assistance.",
                Tips(
                    ("Sécuriser la zone", "Placez-vous à l’écart de la circulation et utilisez la signalisation de sécurité si nécessaire."),
                    ("Décrire le véhicule", "Indiquez le modèle, le type de panne et les voyants éventuellement affichés."),
                    ("Partager la position", "Une adresse ou une position précise facilite l’arrivée du professionnel."))),
            ["nounou"] = Profile(
                "Nounou à domicile à Abidjan : garde d’enfant avec Wélé.",
                "Découvrez les prestations de garde d’enfant à domicile disponibles à Abidjan auprès de professionnels rattachés à des entreprises validées.",
                "Garde ponctuelle ou accompagnement après l’école : consultez les prestations de garde d’enfant proposées dans le catalogue Wélé.",
                "Préparer une demande de garde d’enfant",
                "L’âge de l’enfant, l’horaire et les consignes essentielles doivent être clairement indiqués dans la demande.",
                Tips(
                    ("Préciser l’âge", "Indiquez l’âge de chaque enfant concerné par la garde."),
                    ("Définir l’horaire", "Renseignez précisément le début et la fin souhaités de l’intervention."),
                    ("Partager les consignes", "Mentionnez les habitudes, allergies et contacts utiles au bon déroulement de la garde."))),
            ["climatisation"] = Profile(
                "Climatisation à Abidjan : entretien et dépannage avec Wélé.",
                "Découvrez les prestations de climatisation à Abidjan : entretien, diagnostic, nettoyage et remise en service selon le catalogue Wélé.",
                "Entretien, baisse de performance, bruit ou panne : consultez les prestations de climatisation actives et leurs options sur Wélé.",
                "Décrire le problème de climatisation",
                "La marque, le type d’appareil et les symptômes observés aident à mieux qualifier la demande.",
                Tips(
                    ("Identifier l’appareil", "Indiquez le type de climatiseur et, si possible, sa marque."),
                    ("Décrire le symptôme", "Précisez si l’appareil refroidit mal, fuit, fait du bruit ou ne démarre plus."),
                    ("Faciliter l’accès", "Assurez un accès dégagé à l’unité intérieure et à l’unité extérieure."))),
            ["serrurerie"] = Profile(
                "Serrurier à Abidjan : prestations disponibles avec Wélé.",
                "Découvrez les prestations de serrurerie disponibles à Abidjan et envoyez votre demande depuis l’application Wélé.",
                "Porte, serrure ou clé : consultez les prestations de serrurerie actives proposées par les professionnels référencés sur Wélé.",
                "Décrire votre besoin de serrurerie",
                "Le type de porte, de serrure et la situation rencontrée permettent de mieux orienter la demande.",
                Tips(
                    ("Identifier la porte", "Précisez s’il s’agit d’une porte d’entrée, intérieure, métallique ou d’un portail."),
                    ("Décrire la serrure", "Indiquez si la clé est perdue, cassée ou si la serrure est bloquée."),
                    ("Ajouter une photo", "Une vue nette de la serrure peut aider à qualifier le besoin."))),
            ["coiffure"] = BeautyProfile("Coiffeuse à domicile à Abidjan", "coiffure"),
            ["estheticienne"] = BeautyProfile("Esthéticienne à domicile à Abidjan", "soins esthétiques"),
            ["barbier"] = BeautyProfile("Barbier à domicile à Abidjan", "barbier"),
            ["manucure-et-pedicure"] = BeautyProfile("Manucure et pédicure à domicile à Abidjan", "manucure et pédicure"),
            ["massage-et-bien-etre"] = BeautyProfile("Massage et bien-être à domicile à Abidjan", "massage et bien-être"),
            ["maquillage-professionnel"] = BeautyProfile("Maquillage professionnel à domicile à Abidjan", "maquillage professionnel"),
            ["peinture"] = HomeProfile("Peintre à Abidjan", "peinture", "mur, plafond ou finition"),
            ["electromenager"] = HomeProfile("Dépannage électroménager à Abidjan", "électroménager", "appareil, panne ou diagnostic"),
            ["anti-nuisibles"] = HomeProfile("Traitement anti-nuisibles à Abidjan", "traitement anti-nuisibles", "zone concernée, nuisible observé ou fréquence"),
        };

    public static string ToSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "service";
        }

        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var separatorPending = false;
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                if (separatorPending && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(character);
                separatorPending = false;
            }
            else
            {
                separatorPending = builder.Length > 0;
            }
        }

        return builder.ToString().Trim('-');
    }

    public static ServiceSeoProfile For(ServiceSummaryResponse service)
    {
        var slug = ToSlug(service.Name);
        if (Profiles.TryGetValue(slug, out var exact))
        {
            return exact;
        }

        var match = Profiles.FirstOrDefault(item => slug.Contains(item.Key, StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(match.Key))
        {
            return match.Value;
        }

        var description = string.IsNullOrWhiteSpace(service.Description)
            ? $"Découvrez les prestations de {service.Name.ToLowerInvariant()} disponibles à Abidjan avec Wélé."
            : service.Description.Trim();
        return Profile(
            $"{service.Name} à Abidjan : prestations disponibles avec Wélé.",
            Limit($"{description} Consultez les prestations actives et leurs options dans le catalogue Wélé.", 158),
            description,
            $"Bien préparer votre demande de {service.Name.ToLowerInvariant()}",
            "Une description précise et quelques photos permettent au professionnel de mieux comprendre votre besoin avant l’intervention.",
            Tips(
                ("Décrire le besoin", "Indiquez clairement ce qui doit être réalisé et la zone concernée."),
                ("Ajouter des photos", "Des images nettes permettent de mieux comprendre la situation."),
                ("Choisir la prestation", "Sélectionnez dans l’application la prestation et les options correspondant à votre besoin.")));
    }

    public static IReadOnlyList<ServiceSummaryResponse> PopularServices(
        IEnumerable<ServiceSummaryResponse> services,
        int count = 6) => services
        .Where(service => service.IsActive)
        .OrderBy(service => NavigationRank(ToSlug(service.Name)))
        .ThenBy(service => service.Name, StringComparer.CurrentCultureIgnoreCase)
        .Take(count)
        .ToList();

    public static string NavigationGroup(ServiceSummaryResponse service)
    {
        var slug = ToSlug(service.Name);
        if (slug.Contains("auto", StringComparison.Ordinal)
            || slug.Contains("moto", StringComparison.Ordinal)
            || slug.Contains("transport", StringComparison.Ordinal))
        {
            return "Mobilité";
        }

        if (string.Equals(service.DisplayCategory, "Wellbeing", StringComparison.OrdinalIgnoreCase))
        {
            return "Bien-être";
        }

        if (slug.Contains("nounou", StringComparison.Ordinal)
            || slug.Contains("blanchisserie", StringComparison.Ordinal)
            || slug.Contains("repassage", StringComparison.Ordinal)
            || slug.Contains("livraison", StringComparison.Ordinal))
        {
            return "Quotidien";
        }

        return "Maison";
    }

    public static IReadOnlyList<ServiceSeoFaq> BuildFaqs(
        ServiceSummaryResponse service,
        ServiceSeoProfile profile)
    {
        var activePrestations = service.Prestations.Where(item => item.IsActive).ToList();
        var names = activePrestations.Take(6).Select(item => item.Name).ToList();
        var prestationAnswer = names.Count switch
        {
            0 => $"Les prestations de {service.Name.ToLowerInvariant()} sont affichées dans l’application selon leur disponibilité.",
            1 => $"Le catalogue comprend actuellement la prestation « {names[0]} ».",
            _ => $"Le catalogue comprend notamment : {string.Join(", ", names)}{(activePrestations.Count > names.Count ? " et d’autres prestations actives" : string.Empty)}."
        };

        var optionCount = activePrestations.Sum(prestation => prestation.Options?.Count(option => option.IsActive) ?? 0);
        var optionAnswer = optionCount == 0
            ? "Les variantes éventuelles sont présentées dans l’application au moment de la demande."
            : $"Oui. {optionCount} option{(optionCount > 1 ? "s sont" : " est")} actuellement proposée{(optionCount > 1 ? "s" : string.Empty)} dans le catalogue, sous les prestations concernées.";

        return
        [
            new($"Quelles prestations de {service.Name.ToLowerInvariant()} sont disponibles ?", prestationAnswer),
            new("Existe-t-il plusieurs options ?", optionAnswer),
            new("Wélé intervient-il dans toutes les communes d’Abidjan ?", "La couverture dépend de la zone et de la disponibilité des professionnels. L’application vérifie ces éléments lorsque vous envoyez votre demande."),
            new("Comment envoyer une demande ?", "La demande, le choix des options et le suivi sont réalisés depuis l’application Wélé."),
        ];
    }

    private static ServiceSeoProfile BeautyProfile(string title, string keyword) => Profile(
        $"{title} : prestations avec Wélé.",
        $"Découvrez les prestations de {keyword} disponibles à domicile à Abidjan et retrouvez-les dans l’application Wélé.",
        $"Découvrez les prestations de {keyword} à domicile proposées par les professionnels référencés sur Wélé.",
        "Préparer votre rendez-vous beauté",
        "Le résultat attendu, la durée disponible et les éventuelles contraintes doivent être indiqués clairement dans la demande.",
        Tips(
            ("Choisir la prestation", "Sélectionnez le soin ou la prestation correspondant au résultat recherché."),
            ("Partager une référence", "Une photo d’inspiration peut aider le professionnel à comprendre vos attentes."),
            ("Préciser le rendez-vous", "Choisissez une date, un créneau et une adresse où la prestation pourra se dérouler confortablement.")));

    private static ServiceSeoProfile HomeProfile(string title, string keyword, string details) => Profile(
        $"{title} : prestations avec Wélé.",
        $"Découvrez les prestations de {keyword} disponibles à Abidjan et envoyez votre demande depuis l’application Wélé.",
        $"Consultez toutes les prestations de {keyword} actives et les options proposées dans le catalogue Wélé.",
        $"Bien préparer votre demande de {keyword}",
        $"Décrivez précisément les éléments utiles : {details}.",
        Tips(
            ("Décrire la situation", "Indiquez la zone concernée et le résultat attendu."),
            ("Ajouter des photos", "Des photos nettes facilitent la compréhension du besoin."),
            ("Choisir la prestation", "Sélectionnez la prestation et les options les plus proches de votre besoin.")));

    private static ServiceSeoProfile Profile(
        string title,
        string metaDescription,
        string introduction,
        string editorialTitle,
        string editorialText,
        IReadOnlyList<ServiceSeoTip> tips) =>
        new(title, Limit(metaDescription, 158), introduction, editorialTitle, editorialText, tips);

    private static IReadOnlyList<ServiceSeoTip> Tips(params (string Title, string Text)[] values) =>
        values.Select(value => new ServiceSeoTip(value.Title, value.Text)).ToList();

    private static string Limit(string value, int maxLength) =>
        value.Length <= maxLength ? value : $"{value[..(maxLength - 1)].TrimEnd()}…";

    private static int NavigationRank(string slug)
    {
        for (var index = 0; index < NavigationPriority.Length; index++)
        {
            if (slug.Contains(NavigationPriority[index], StringComparison.Ordinal))
            {
                return index;
            }
        }

        return NavigationPriority.Length;
    }
}

public sealed record ServiceSeoProfile(
    string Title,
    string MetaDescription,
    string Introduction,
    string EditorialTitle,
    string EditorialText,
    IReadOnlyList<ServiceSeoTip> Tips);

public sealed record ServiceSeoTip(string Title, string Text);

public sealed record ServiceSeoFaq(string Question, string Answer);
