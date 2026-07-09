using HomeService.Domain.Entities;
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
        await SeedLanguagesAsync(db, cancellationToken);
        await SeedServicesAsync(db, cancellationToken);
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
}
