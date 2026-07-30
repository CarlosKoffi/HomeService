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

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase($"client-catalog-{Guid.NewGuid():N}")
            .Options;
        return new HomeServiceDbContext(options);
    }
}
