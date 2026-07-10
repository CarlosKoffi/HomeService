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

        db.Services.AddRange(
            new Service("Menage a domicile", "Entretien courant du domicile, nettoyage, rangement et aide ponctuelle.", createdByCompanyId: null),
            new Service("Nounou", "Garde d'enfant a domicile par un prestataire recommande et rattache a une entreprise validee.", createdByCompanyId: null),
            new Service("Jardinage", "Entretien jardin, taille simple, arrosage et travaux exterieurs legers.", createdByCompanyId: null));

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

    private sealed record TranslationSeed(string Key, string Scope, string Description, string Value);
}
