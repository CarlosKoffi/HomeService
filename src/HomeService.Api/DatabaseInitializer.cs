using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Api;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HomeServiceDbContext>();

        await db.Database.MigrateAsync(cancellationToken);
        await SeedCountriesAsync(db, cancellationToken);
        await SeedCountryBrandingAsync(db, cancellationToken);
        await SeedLanguagesAsync(db, cancellationToken);
        await SeedServicesAsync(db, cancellationToken);
        await SeedServicePrestationsAsync(db, cancellationToken);
        await SeedAdminAccessAsync(db, cancellationToken);
        await SeedTranslationsAsync(db, cancellationToken);
        await SeedCmsFoundationAsync(db, cancellationToken);
        await SeedCompanyEditorialContentAsync(db, cancellationToken);
    }

    private static async Task SeedCountriesAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Countries.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Countries.AddRange(
            new Country("CI", "Cote d'Ivoire", "XOF", isLaunchCountry: true),
            new Country("SN", "Senegal", "XOF"),
            new Country("BJ", "Benin", "XOF"),
            new Country("TG", "Togo", "XOF"));

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedLanguagesAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Languages.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Languages.AddRange(
            new Language("fr", "Francais", isDefault: true),
            new Language("en", "Anglais"));

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedCountryBrandingAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        var coteDIvoire = await db.Countries.FirstAsync(country => country.IsoCode == "CI", cancellationToken);
        var hasBranding = await db.CountryBrandings.AnyAsync(branding => branding.CountryId == coteDIvoire.Id, cancellationToken);
        if (hasBranding)
        {
            return;
        }

        db.CountryBrandings.Add(new CountryBranding(
            coteDIvoire.Id,
            "ProxiPro CI",
            "#0f9f7a",
            "#ffffff",
            "#f97316",
            "Le service a domicile en toute confiance",
            "Une plateforme pensee pour la Cote d'Ivoire: entreprises verifiees, prestataires suivis et services a domicile plus fiables.",
            null,
            "flag-ribbon"));

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedServicesAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        var seeds = new[]
        {
            new SeededService("Menage a domicile", "Entretien courant du domicile, nettoyage, rangement et aide ponctuelle.", "sparkles", 3500, 5000, "XOF"),
            new SeededService("Jardinage", "Entretien jardin, taille simple, arrosage et travaux exterieurs legers.", "sprout", 4500, 6500, "XOF"),
            new SeededService("Electricite", "Petites interventions electriques, diagnostic simple et remise en service.", "zap", 5000, 8000, "XOF"),
            new SeededService("Blanchisserie", "Lavage, repassage et entretien du linge pour particuliers et familles.", "shirt", 2500, 4500, "XOF"),
            new SeededService("Depannage auto", "Assistance auto de proximite pour les urgences simples et depannages courants.", "car", 7000, 12000, "XOF"),
            new SeededService("Nounou", "Garde d'enfant a domicile par un prestataire recommande et rattache a une entreprise validee.", "baby", 4000, 6500, "XOF")
        };

        var existingServices = await db.Services.ToListAsync(cancellationToken);
        foreach (var seed in seeds)
        {
            var normalizedName = NormalizeSeedValue(seed.Name);
            var service = existingServices.FirstOrDefault(item => item.NormalizedName == normalizedName);
            if (service is null)
            {
                service = new Service(seed.Name, seed.Description, createdByCompanyId: null);
                db.Services.Add(service);
                existingServices.Add(service);
            }

            service.UpdatePricing(seed.NormalPriceAmount, seed.PremiumPriceAmount, seed.Currency);
            service.UpdateIcon(seed.IconName);
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedServicePrestationsAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        var services = await db.Services
            .Where(service => service.NormalizedName == "jardinage"
                || service.NormalizedName == "menage a domicile"
                || service.NormalizedName == "nounou"
                || service.NormalizedName == "electricite"
                || service.NormalizedName == "blanchisserie"
                || service.NormalizedName == "depannage auto")
            .Select(service => new { service.Id, service.NormalizedName })
            .ToListAsync(cancellationToken);

        if (services.Count == 0)
        {
            return;
        }

        var serviceIds = services.Select(service => service.Id).ToArray();
        var existingPrestations = await db.ServicePrestations
            .Where(prestation => serviceIds.Contains(prestation.ServiceId))
            .ToListAsync(cancellationToken);

        var existingKeySet = existingPrestations
            .Select(prestation => $"{prestation.ServiceId:N}:{prestation.NormalizedName}")
            .ToHashSet(StringComparer.Ordinal);

        var seeds = new[]
        {
            new SeededServicePrestation("jardinage", "Tondre le gazon", "Coupe et entretien simple de pelouse.", 10, 4500, 6500, "XOF"),
            new SeededServicePrestation("jardinage", "Tailler une haie", "Taille legere et remise en forme des haies.", 20, 5500, 7500, "XOF"),
            new SeededServicePrestation("jardinage", "Desherbage", "Nettoyage des mauvaises herbes sur les zones indiquees.", 30, 3500, 5000, "XOF"),
            new SeededServicePrestation("jardinage", "Arrosage et entretien plantes", "Arrosage, controle visuel et entretien leger des plantes.", 40, 3000, 4500, "XOF"),
            new SeededServicePrestation("jardinage", "Ramassage feuilles", "Ramassage des feuilles et nettoyage leger des allees.", 50, 3000, 4500, "XOF"),
            new SeededServicePrestation("jardinage", "Nettoyage terrasse exterieure", "Balayage et nettoyage simple de terrasse ou cour.", 60, 4500, 6500, "XOF"),
            new SeededServicePrestation("menage a domicile", "Menage regulier", "Entretien courant du domicile.", 10, 3500, 5000, "XOF"),
            new SeededServicePrestation("menage a domicile", "Grand nettoyage", "Nettoyage complet d'un logement ou d'une grande piece.", 15, 6000, 8500, "XOF"),
            new SeededServicePrestation("menage a domicile", "Nettoyage apres travaux", "Nettoyage renforce apres petits travaux ou renovation.", 20, 5000, 7000, "XOF"),
            new SeededServicePrestation("menage a domicile", "Nettoyage vitres", "Nettoyage simple des vitres accessibles.", 30, 3000, 4500, "XOF"),
            new SeededServicePrestation("menage a domicile", "Nettoyage cuisine", "Nettoyage detaille de cuisine, plans de travail et surfaces.", 40, 4000, 6000, "XOF"),
            new SeededServicePrestation("menage a domicile", "Nettoyage sanitaires", "Nettoyage detaille salle d'eau, WC et surfaces sanitaires.", 50, 4000, 6000, "XOF"),
            new SeededServicePrestation("nounou", "Garde ponctuelle", "Garde d'enfant sur une plage horaire courte.", 10, 4000, 6500, "XOF"),
            new SeededServicePrestation("nounou", "Garde apres ecole", "Presence et accompagnement apres l'ecole.", 20, 4500, 7000, "XOF"),
            new SeededServicePrestation("electricite", "Diagnostic panne electrique", "Recherche simple de panne et conseil d'intervention.", 10, 6000, 9000, "XOF"),
            new SeededServicePrestation("electricite", "Remplacement prise ou interrupteur", "Remplacement d'une prise, interrupteur ou point simple.", 20, 5000, 7500, "XOF"),
            new SeededServicePrestation("electricite", "Installation luminaire", "Pose ou remplacement d'un luminaire existant.", 30, 6000, 9000, "XOF"),
            new SeededServicePrestation("electricite", "Remise en service disjoncteur", "Controle et remise en service simple apres coupure.", 40, 5000, 8000, "XOF"),
            new SeededServicePrestation("electricite", "Depannage court-circuit simple", "Intervention sur panne courte et localisee.", 50, 8000, 12000, "XOF"),
            new SeededServicePrestation("electricite", "Installation ventilateur plafond", "Pose simple d'un ventilateur sur attente electrique existante.", 60, 10000, 15000, "XOF"),
            new SeededServicePrestation("blanchisserie", "Lavage et pliage", "Lavage, sechage et pliage du linge courant.", 10, 2500, 4000, "XOF"),
            new SeededServicePrestation("blanchisserie", "Repassage", "Repassage de vetements courants.", 20, 3000, 4500, "XOF"),
            new SeededServicePrestation("blanchisserie", "Linge de maison", "Entretien draps, serviettes et linge de maison.", 30, 3500, 5500, "XOF"),
            new SeededServicePrestation("blanchisserie", "Pressing tenue", "Entretien de tenue, robe, chemise ou costume selon disponibilite.", 40, 5000, 8000, "XOF"),
            new SeededServicePrestation("blanchisserie", "Detache simple", "Traitement simple de tache avant lavage.", 50, 3000, 5000, "XOF"),
            new SeededServicePrestation("depannage auto", "Changement batterie", "Remplacement ou assistance batterie sur place.", 10, 7000, 12000, "XOF"),
            new SeededServicePrestation("depannage auto", "Aide crevaison", "Aide au changement de roue ou pose de roue de secours.", 20, 6000, 10000, "XOF"),
            new SeededServicePrestation("depannage auto", "Demarrage avec cables", "Assistance demarrage avec cables ou booster.", 30, 6000, 9000, "XOF"),
            new SeededServicePrestation("depannage auto", "Diagnostic panne demarrage", "Controle simple quand le vehicule ne demarre pas.", 40, 8000, 12000, "XOF"),
            new SeededServicePrestation("depannage auto", "Carburant urgence", "Assistance en cas de panne seche dans la zone couverte.", 50, 6000, 10000, "XOF"),
            new SeededServicePrestation("depannage auto", "Remorquage partenaire", "Mise en relation ou assistance remorquage selon disponibilite.", 60, 15000, 25000, "XOF")
        };

        foreach (var seed in seeds)
        {
            var service = services.FirstOrDefault(item => item.NormalizedName == seed.ServiceNormalizedName);
            if (service is null)
            {
                continue;
            }

            var normalizedPrestationName = NormalizeSeedValue(seed.Name);
            var existing = existingPrestations.FirstOrDefault(prestation =>
                prestation.ServiceId == service.Id && prestation.NormalizedName == normalizedPrestationName);
            if (existing is not null)
            {
                existing.UpdatePricing(seed.NormalPriceAmount, seed.PremiumPriceAmount, seed.Currency);
                continue;
            }

            db.ServicePrestations.Add(new ServicePrestation(
                service.Id,
                seed.Name,
                seed.Description,
                seed.SortOrder,
                seed.NormalPriceAmount,
                seed.PremiumPriceAmount,
                seed.Currency));
            existingKeySet.Add($"{service.Id:N}:{normalizedPrestationName}");
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static string NormalizeSeedValue(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private sealed record SeededService(
        string Name,
        string Description,
        string IconName,
        int NormalPriceAmount,
        int PremiumPriceAmount,
        string Currency);

    private sealed record SeededServicePrestation(
        string ServiceNormalizedName,
        string Name,
        string? Description,
        int SortOrder,
        int NormalPriceAmount,
        int PremiumPriceAmount,
        string Currency);

    private static async Task SeedAdminAccessAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.AdminModules.AnyAsync(cancellationToken))
        {
            db.AdminModules.AddRange(
                new AdminModule(AdminModuleKey.Dashboard, "Tableau de bord", "Vue de synthese du back-office entreprise.", 10),
                new AdminModule(AdminModuleKey.CompanyApplications, "Demandes entreprises", "Validation des inscriptions, documents et activation des entreprises.", 20),
                new AdminModule(AdminModuleKey.Services, "Services", "Gestion du catalogue plat et des services proposes par les entreprises.", 30),
                new AdminModule(AdminModuleKey.Localization, "Pays et traductions", "Gestion des pays, langues et textes traduisibles.", 40),
                new AdminModule(AdminModuleKey.AdminAccess, "Acces et roles", "Gestion des roles, modules et permissions admin.", 50));

            await db.SaveChangesAsync(cancellationToken);
        }

        if (!await db.AdminRoles.AnyAsync(cancellationToken))
        {
            db.AdminRoles.AddRange(
                new AdminRole("Super admin", "Acces complet a tous les modules et aux permissions."),
                new AdminRole("Validation entreprises", "Peut traiter les demandes d'inscription entreprise."),
                new AdminRole("Contenu et traduction", "Peut gerer les textes, pays et langues."));

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedTranslationsAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        var french = await db.Languages.FirstAsync(language => language.Code == "fr", cancellationToken);
        var coteDIvoire = await db.Countries.FirstAsync(country => country.IsoCode == "CI", cancellationToken);

        var translations = new[]
        {
            new TranslationSeed("company.home.hero.title", "Company", "Titre hero portail entreprise", "Le service a domicile en toute confiance"),
            new TranslationSeed("company.home.hero.subtitle", "Company", "Sous-titre hero portail entreprise", "Inscrivez votre entreprise, faites valider vos prestataires et developpez vos missions a domicile."),
            new TranslationSeed("company.register.title", "Company", "Titre inscription entreprise", "Demande d'inscription"),
            new TranslationSeed("company.register.description", "Company", "Introduction formulaire inscription", "Ce formulaire permet a votre entreprise de demander son acces ProxiPro. Notre equipe verifiera les informations et les pieces fournies."),
            new TranslationSeed("company.register.submit", "Company", "Bouton envoyer demande", "Envoyer la demande"),
            new TranslationSeed("company.register.success", "Company", "Confirmation demande envoyee", "Demande envoyee. Notre equipe va verifier votre dossier."),
            new TranslationSeed("company.employees.form.title", "Company", "Titre formulaire ajout employe", "Nouvel employe"),
            new TranslationSeed("company.employees.form.description", "Company", "Aide formulaire ajout employe", "Renseignez les informations essentielles. Les prix des services sont fixes par la plateforme."),
            new TranslationSeed("company.employees.services.title", "Company", "Titre selection services employe", "Services maitrises"),
            new TranslationSeed("company.employees.services.description", "Company", "Aide selection services employe", "Selectionnez les services que ce prestataire peut realiser. Les tarifs normal et premium sont fixes dans l'administration."),
            new TranslationSeed("company.employees.upload.photo", "Company", "Upload photo employe", "Photo du prestataire"),
            new TranslationSeed("company.employees.upload.identity", "Company", "Upload piece identite employe", "Piece d'identite"),
            new TranslationSeed("company.employees.upload.diploma", "Company", "Upload diplome employe", "Diplome ou certificat"),
            new TranslationSeed("company.employees.upload.choose", "Company", "Bouton selection fichier employe", "Selectionner"),
            new TranslationSeed("admin.dashboard.title", "Admin", "Titre dashboard admin", "Centre de controle entreprise"),
            new TranslationSeed("admin.companyApplications.title", "Admin", "Titre file demandes entreprise", "Demandes entreprises"),
            new TranslationSeed("admin.companyApplications.empty", "Admin", "Message liste vide", "Aucune demande entreprise pour le moment."),
            new TranslationSeed("admin.localization.title", "Admin", "Titre page traductions", "Pays & traductions"),
            new TranslationSeed("admin.access.title", "Admin", "Titre acces roles", "Acces & roles"),
            new TranslationSeed("common.loading", "Common", "Message chargement generique", "Chargement en cours..."),
            new TranslationSeed("common.save", "Common", "Action sauvegarder", "Sauver"),
            new TranslationSeed("common.validate", "Common", "Action valider", "Valider"),
            new TranslationSeed("common.reject", "Common", "Action rejeter", "Rejeter")
        };

        foreach (var seed in translations)
        {
            var key = await db.TranslationKeys
                .Include(item => item.Values)
                .FirstOrDefaultAsync(item => item.Key == seed.Key, cancellationToken);

            if (key is null)
            {
                key = new TranslationKey(seed.Key, seed.Description, seed.Scope);
                db.TranslationKeys.Add(key);
                await db.SaveChangesAsync(cancellationToken);
            }

            var hasValue = await db.TranslationValues.AnyAsync(
                value => value.TranslationKeyId == key.Id
                    && value.LanguageId == french.Id
                    && value.CountryId == coteDIvoire.Id,
                cancellationToken);

            if (!hasValue)
            {
                db.TranslationValues.Add(new TranslationValue(key.Id, french.Id, coteDIvoire.Id, seed.Value));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedCmsFoundationAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        if (await db.CmsSites.AnyAsync(cancellationToken))
        {
            return;
        }

        var french = await db.Languages.FirstAsync(language => language.Code == "fr", cancellationToken);
        var coteDIvoire = await db.Countries.FirstAsync(country => country.IsoCode == "CI", cancellationToken);

        var hero = new CmsComponentDefinition("HeroStandard", "Hero standard", 1, "Section d'ouverture sobre avec titre, texte court et appels a l'action.");
        var steps = new CmsComponentDefinition("StepsTimeline", "Parcours en etapes", 1, "Explication courte d'un processus en trois a six etapes.");
        var trusted = new CmsComponentDefinition("TrustedLogos", "Preuve sociale", 1, "Bande de references ou preuves de confiance en style premium.");
        var services = new CmsComponentDefinition("ServicesList", "Liste de services", 1, "Liste structuree de services ou metiers affichables.");
        var dashboard = new CmsComponentDefinition("DashboardPreview", "Apercu dashboard", 1, "Mockup produit avec indicateurs, activite et donnees de demonstration.");
        var faq = new CmsComponentDefinition("FaqAccordion", "Foire aux questions", 1, "Questions/reponses simples avec ouverture progressive.");
        var cta = new CmsComponentDefinition("CallToAction", "Appel a l'action", 1, "Bloc final ou contextuel pour pousser une action principale.");
        var contact = new CmsComponentDefinition("ContactForm", "Formulaire de contact", 1, "Formulaire editorial de prise de contact.");
        var footer = new CmsComponentDefinition("FooterLinks", "Liens footer", 1, "Colonnes de liens de bas de page.");

        db.CmsComponentDefinitions.AddRange(hero, steps, trusted, services, dashboard, faq, cta, contact, footer);

        var companySite = new CmsSite("company-public", "Kaza entreprises", CmsSiteSurface.PublicCompany, coteDIvoire.Id, french.Id);
        companySite.Activate();
        companySite.SetHomePage("home");

        var providerSite = new CmsSite("provider-public", "Kaza prestataires", CmsSiteSurface.PublicProvider, coteDIvoire.Id, french.Id);
        providerSite.Activate();
        providerSite.SetHomePage("home");

        var clientSite = new CmsSite("client-public", "Kaza clients", CmsSiteSurface.PublicClient, coteDIvoire.Id, french.Id);
        clientSite.Activate();
        clientSite.SetHomePage("home");

        var companyPortal = new CmsSite("company-portal", "Portail entreprise", CmsSiteSurface.CompanyPortal, coteDIvoire.Id, french.Id);
        companyPortal.Activate();
        companyPortal.SetHomePage("dashboard");

        db.CmsSites.AddRange(companySite, providerSite, clientSite, companyPortal);

        AddSeedPage(db, companySite, french.Id, "home", "Accueil entreprises", "premium-b2b-landing", "entreprises", "Kaza pour les entreprises", hero.Id, steps.Id, trusted.Id, dashboard.Id, faq.Id, contact.Id, footer.Id);
        AddSeedPage(db, providerSite, french.Id, "home", "Accueil prestataires", "landing", "prestataires", "Kaza pour les prestataires", hero.Id, steps.Id, faq.Id);
        AddSeedPage(db, clientSite, french.Id, "home", "Accueil clients", "landing", "accueil", "Kaza", hero.Id, services.Id, faq.Id);
        AddSeedPage(db, companyPortal, french.Id, "dashboard", "Tableau de bord entreprise", "portal-dashboard", "dashboard", "Tableau de bord", cta.Id);

        db.CmsMenus.AddRange(
            new CmsMenu(companySite.Id, "main", "Menu principal", "header"),
            new CmsMenu(companySite.Id, "footer", "Pied de page", "footer"),
            new CmsMenu(providerSite.Id, "main", "Menu principal", "header"),
            new CmsMenu(clientSite.Id, "main", "Menu principal", "header"),
            new CmsMenu(companyPortal.Id, "portal", "Navigation portail", "sidebar"));

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedCompanyEditorialContentAsync(HomeServiceDbContext db, CancellationToken cancellationToken)
    {
        var french = await db.Languages.FirstAsync(language => language.Code == "fr", cancellationToken);
        var homePage = await db.CmsPages
            .Include(page => page.Site)
            .Include(page => page.Versions)
                .ThenInclude(version => version.Sections)
                    .ThenInclude(section => section.ComponentDefinition)
            .Include(page => page.Versions)
                .ThenInclude(version => version.Sections)
                    .ThenInclude(section => section.ContentValues)
            .Where(page => page.Site!.Code == "company-public" && page.Code == "home")
            .FirstOrDefaultAsync(cancellationToken);

        var version = homePage?.Versions.OrderByDescending(item => item.VersionNumber).FirstOrDefault();
        if (version is null)
        {
            return;
        }

        foreach (var section in version.Sections)
        {
            switch (section.ComponentDefinition?.Key)
            {
                case "HeroStandard":
                    AddCmsText(db, section, "label", CmsContentValueType.ShortText, "Plateforme partenaire", french.Id);
                    AddCmsText(db, section, "headline", CmsContentValueType.ShortText, "Recevez plus de missions. Developpez votre entreprise.", french.Id);
                    AddCmsText(db, section, "subtitle", CmsContentValueType.LongText, "Kaza connecte les clients aux entreprises de services a domicile verifiees. Vous gardez le controle de vos equipes, de vos demandes et de vos interventions.", french.Id);
                    AddCmsText(db, section, "primaryCta.label", CmsContentValueType.ShortText, "Commencer", french.Id);
                    AddCmsText(db, section, "primaryCta.url", CmsContentValueType.InternalLink, "register", french.Id);
                    AddCmsText(db, section, "secondaryCta.label", CmsContentValueType.ShortText, "Voir le fonctionnement", french.Id);
                    AddCmsText(db, section, "secondaryCta.url", CmsContentValueType.InternalLink, "#how", french.Id);
                    AddCmsText(db, section, "image.url", CmsContentValueType.Media, "images/kaza-premium-hero.png", french.Id);
                    AddCmsText(db, section, "image.alt", CmsContentValueType.ShortText, "Equipe Kaza en intervention chez un client", french.Id);
                    AddCmsJson(db, section, "proofItems", "[\"Inscription gratuite\",\"Validation dossier\",\"Portail entreprise\"]", french.Id);
                    break;

                case "StepsTimeline":
                    AddCmsText(db, section, "label", CmsContentValueType.ShortText, "Comment ca marche", french.Id);
                    AddCmsText(db, section, "headline", CmsContentValueType.ShortText, "Trois etapes, puis votre portail est pret.", french.Id);
                    AddCmsText(db, section, "subtitle", CmsContentValueType.LongText, "Un parcours court pour verifier l'entreprise et demarrer avec une base claire.", french.Id);
                    AddCmsJson(db, section, "steps", """
                    [
                      {"number":"01","label":"Compte","title":"Creez votre compte","text":"Renseignez votre entreprise, vos services et le contact responsable.","image":"images/kaza-how-step-1.png"},
                      {"number":"02","label":"Verification","title":"Nous verifions votre dossier","text":"Kaza controle les informations pour securiser les clients et les missions.","image":"images/kaza-how-step-2.png"},
                      {"number":"03","label":"Portail","title":"Travaillez depuis votre portail","text":"Ajoutez vos prestataires, recevez des demandes et suivez vos interventions.","image":"images/kaza-how-step-3.png"}
                    ]
                    """, french.Id);
                    break;

                case "TrustedLogos":
                    AddCmsText(db, section, "headline", CmsContentValueType.ShortText, "Ils font confiance a Kaza", french.Id);
                    AddCmsJson(db, section, "items", "[\"Services verifies\",\"Entreprises locales\",\"Prestataires suivis\",\"Paiements traces\",\"Support partenaire\"]", french.Id);
                    break;

                case "DashboardPreview":
                    AddCmsText(db, section, "label", CmsContentValueType.ShortText, "Dashboard", french.Id);
                    AddCmsText(db, section, "headline", CmsContentValueType.ShortText, "Tout ce qui compte, lisible en un coup d'oeil.", french.Id);
                    AddCmsText(db, section, "subtitle", CmsContentValueType.LongText, "Demandes, equipe, missions, documents et paiements restent au meme endroit.", french.Id);
                    AddCmsJson(db, section, "stats", "[{\"label\":\"Demandes\",\"value\":\"12\",\"help\":\"+4 cette semaine\"},{\"label\":\"Assignees\",\"value\":\"8\",\"help\":\"Equipe mobilisee\"},{\"label\":\"Paiements\",\"value\":\"185k\",\"help\":\"XOF suivis\"}]", french.Id);
                    AddCmsJson(db, section, "requests", "[\"Menage a Cocody Riviera\",\"Jardinage a Marcory\",\"Nounou aux Deux Plateaux\"]", french.Id);
                    AddCmsJson(db, section, "providers", "[\"Awa K. - Menage\",\"Jean M. - Jardinage\",\"Fatou C. - Nounou\"]", french.Id);
                    break;

                case "FaqAccordion":
                    AddCmsText(db, section, "label", CmsContentValueType.ShortText, "FAQ", french.Id);
                    AddCmsText(db, section, "headline", CmsContentValueType.ShortText, "Foire aux questions", french.Id);
                    AddCmsJson(db, section, "questions", """
                    [
                      {"question":"Comment sont verifiees les entreprises sur Kaza ?","answer":"Nous verifions les informations de l'entreprise, les documents essentiels et le contact responsable avant l'activation complete."},
                      {"question":"L'inscription est-elle gratuite ?","answer":"Oui. L'inscription est gratuite. Kaza applique ensuite une commission uniquement sur les missions realisees."},
                      {"question":"Puis-je refuser une demande client ?","answer":"Oui. Votre entreprise reste libre d'accepter les demandes qui correspondent a son equipe, sa zone et ses disponibilites."},
                      {"question":"Qui choisit le prestataire ?","answer":"Vous pouvez affecter vous-meme un prestataire depuis le portail ou laisser Kaza vous accompagner selon le mode choisi."},
                      {"question":"Comment sont suivis les paiements ?","answer":"Le portail permet de suivre les paiements Mobile Money, les encaissements terrain et les commissions."},
                      {"question":"Combien de temps prend la validation ?","answer":"Elle depend de la qualite du dossier. Plus les informations sont claires, plus la validation est rapide."}
                    ]
                    """, french.Id);
                    break;

                case "ContactForm":
                    AddCmsText(db, section, "label", CmsContentValueType.ShortText, "Contact", french.Id);
                    AddCmsText(db, section, "headline", CmsContentValueType.ShortText, "Vous voulez en parler avant de vous inscrire ?", french.Id);
                    AddCmsText(db, section, "subtitle", CmsContentValueType.LongText, "Laissez vos coordonnees. Nous vous rappelons pour voir comment Kaza peut aider votre entreprise.", french.Id);
                    AddCmsJson(db, section, "tags", "[\"Abidjan\",\"Services a domicile\",\"Partenariat entreprise\"]", french.Id);
                    break;

                case "FooterLinks":
                    AddCmsText(db, section, "brandText", CmsContentValueType.LongText, "La plateforme B2B pour connecter clients, entreprises et professionnels de confiance.", french.Id);
                    AddCmsText(db, section, "copyright", CmsContentValueType.ShortText, "© 2026 Kaza Technologies. Tous droits reserves.", french.Id);
                    AddCmsText(db, section, "baseline", CmsContentValueType.ShortText, "Concu pour l'Afrique de l'Ouest", french.Id);
                    AddCmsJson(db, section, "columns", """
                    [
                      {"title":"Produit","links":["Plateforme","Fonctionnement","Tarifs","Securite","Integrations","Changelog"]},
                      {"title":"Entreprise","links":["A propos","Blog","Carrieres","Presse","Partenaires"]},
                      {"title":"Ressources","links":["Documentation","Centre d'aide","Communaute","Dashboard","Etudes de cas"]},
                      {"title":"Legal","links":["CGU","Confidentialite","Cookies","Mentions legales","Conditions partenaires"]}
                    ]
                    """, french.Id);
                    break;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static void AddSeedPage(
        HomeServiceDbContext db,
        CmsSite site,
        Guid languageId,
        string code,
        string internalName,
        string templateKey,
        string slug,
        string title,
        params Guid[] componentDefinitionIds)
    {
        var page = new CmsPage(site.Id, code, internalName, templateKey);
        var translation = new CmsPageTranslation(site.Id, page.Id, languageId, slug, title);
        var version = new CmsPageVersion(page.Id, 1);

        db.CmsPages.Add(page);
        db.CmsPageTranslations.Add(translation);
        db.CmsPageVersions.Add(version);

        for (var index = 0; index < componentDefinitionIds.Length; index++)
        {
            db.CmsSections.Add(new CmsSection(
                version.Id,
                componentDefinitionIds[index],
                $"{internalName} - section {index + 1}",
                "main",
                index + 1));
        }
    }

    private static void AddCmsText(
        HomeServiceDbContext db,
        CmsSection section,
        string fieldKey,
        CmsContentValueType valueType,
        string value,
        Guid languageId)
    {
        if (section.ContentValues.Any(item => item.FieldKey == fieldKey && item.LanguageId == languageId))
        {
            return;
        }

        var contentValue = new CmsContentValue(section.Id, fieldKey, valueType, languageId);
        contentValue.SetText(value);
        db.CmsContentValues.Add(contentValue);
    }

    private static void AddCmsJson(
        HomeServiceDbContext db,
        CmsSection section,
        string fieldKey,
        string value,
        Guid languageId)
    {
        if (section.ContentValues.Any(item => item.FieldKey == fieldKey && item.LanguageId == languageId))
        {
            return;
        }

        var contentValue = new CmsContentValue(section.Id, fieldKey, CmsContentValueType.Json, languageId);
        contentValue.SetJson(value);
        db.CmsContentValues.Add(contentValue);
    }

    private sealed record TranslationSeed(string Key, string Scope, string Description, string Value);
}
