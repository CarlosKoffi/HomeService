using HomeService.Application.Clients;
using HomeService.Domain.Entities;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class ClientCatalogSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_WhenQueryMatchesPrestation_ReturnsParentServiceAndPrestation()
    {
        await using var db = CreateDbContext();
        var service = new Service("Blanchisserie", "Linge et repassage", createdByCompanyId: null);
        service.AddPrestation("Repassage", "Repassage chemises et pantalons", 1, 1_500, 5_000);
        db.Services.Add(service);
        await db.SaveChangesAsync();
        var sut = new ClientCatalogSearchService(db);

        var results = await sut.SearchAsync("repassage", CancellationToken.None);

        Assert.Contains(results, result => result.Type == "Prestation" && result.ServiceName == "Blanchisserie");
    }

    [Fact]
    public async Task SearchAsync_IsAccentTolerant()
    {
        await using var db = CreateDbContext();
        db.Services.Add(new Service("Ménage à domicile", "Nettoyage maison", createdByCompanyId: null));
        await db.SaveChangesAsync();
        var sut = new ClientCatalogSearchService(db);

        var results = await sut.SearchAsync("menage", CancellationToken.None);

        Assert.Contains(results, result => result.Name == "Ménage à domicile");
    }

    [Fact]
    public async Task SearchAsync_WhenPrestationHasIllustration_ReturnsItsOwnImage()
    {
        await using var db = CreateDbContext();
        var service = new Service("Coiffure", "Soins capillaires", createdByCompanyId: null);
        var prestation = service.AddPrestation("Tresses collees", "Coiffure avec rajouts", 1, 5_000, 12_000);
        prestation.UpdateIllustration("/assets/prestations/tresses-collees.jpg");
        db.Services.Add(service);
        await db.SaveChangesAsync();
        var sut = new ClientCatalogSearchService(db);

        var results = await sut.SearchAsync("tresses", CancellationToken.None);

        var result = Assert.Single(results, item => item.Type == "Prestation");
        Assert.Equal("/assets/prestations/tresses-collees.jpg", result.ImageUrl);
    }

    [Theory]
    [InlineData("plomberie")]
    [InlineData("evier")]
    public async Task SearchAsync_WhenQueryMatchesPlumbingCatalog_ReturnsResult(string query)
    {
        await using var db = CreateDbContext();
        var service = new Service("Plomberie", "Fuites et installations sanitaires", createdByCompanyId: null);
        service.AddPrestation("Deboucher un evier", "Debouchage simple", 10, 6_000, 10_000);
        db.Services.Add(service);
        await db.SaveChangesAsync();
        var sut = new ClientCatalogSearchService(db);

        var results = await sut.SearchAsync(query, CancellationToken.None);

        Assert.NotEmpty(results);
        Assert.Contains(results, result => result.ServiceName == "Plomberie");
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase($"client-catalog-{Guid.NewGuid():N}")
            .Options;
        return new HomeServiceDbContext(options);
    }
}
