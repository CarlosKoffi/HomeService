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
        await SeedAdminAccessAsync(db, cancellationToken);
        await SeedTranslationsAsync(db, cancellationToken);
        await SeedCmsFoundationAsync(db, cancellationToken);
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
        if (await db.Services.AnyAsync(cancellationToken))
        {
            return;
        }

        var menage = new Service("Menage a domicile", "Entretien courant du domicile, nettoyage, rangement et aide ponctuelle.", createdByCompanyId: null);
        menage.UpdatePricing(3500, 5000, "XOF");
        var nounou = new Service("Nounou", "Garde d'enfant a domicile par un prestataire recommande et rattache a une entreprise validee.", createdByCompanyId: null);
        nounou.UpdatePricing(4000, 6500, "XOF");
        var jardinage = new Service("Jardinage", "Entretien jardin, taille simple, arrosage et travaux exterieurs legers.", createdByCompanyId: null);
        jardinage.UpdatePricing(4500, 6500, "XOF");

        db.Services.AddRange(menage, nounou, jardinage);

        await db.SaveChangesAsync(cancellationToken);
    }

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
        var services = new CmsComponentDefinition("ServicesList", "Liste de services", 1, "Liste structuree de services ou metiers affichables.");
        var faq = new CmsComponentDefinition("FaqAccordion", "Foire aux questions", 1, "Questions/reponses simples avec ouverture progressive.");
        var cta = new CmsComponentDefinition("CallToAction", "Appel a l'action", 1, "Bloc final ou contextuel pour pousser une action principale.");
        var contact = new CmsComponentDefinition("ContactForm", "Formulaire de contact", 1, "Formulaire editorial de prise de contact.");

        db.CmsComponentDefinitions.AddRange(hero, steps, services, faq, cta, contact);

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

        AddSeedPage(db, companySite, french.Id, "home", "Accueil entreprises", "landing", "entreprises", "Kaza pour les entreprises", hero.Id, steps.Id, services.Id, faq.Id, contact.Id);
        AddSeedPage(db, providerSite, french.Id, "home", "Accueil prestataires", "landing", "prestataires", "Kaza pour les prestataires", hero.Id, steps.Id, faq.Id);
        AddSeedPage(db, clientSite, french.Id, "home", "Accueil clients", "landing", "", "Kaza", hero.Id, services.Id, faq.Id);
        AddSeedPage(db, companyPortal, french.Id, "dashboard", "Tableau de bord entreprise", "portal-dashboard", "dashboard", "Tableau de bord", cta.Id);

        db.CmsMenus.AddRange(
            new CmsMenu(companySite.Id, "main", "Menu principal", "header"),
            new CmsMenu(companySite.Id, "footer", "Pied de page", "footer"),
            new CmsMenu(providerSite.Id, "main", "Menu principal", "header"),
            new CmsMenu(clientSite.Id, "main", "Menu principal", "header"),
            new CmsMenu(companyPortal.Id, "portal", "Navigation portail", "sidebar"));

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

    private sealed record TranslationSeed(string Key, string Scope, string Description, string Value);
}
