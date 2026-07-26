using HomeService.Application.Admin;
using HomeService.Contracts.Services;
using HomeService.Domain.Entities;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminServiceCatalogManagementServiceTests
{
    [Fact]
    public async Task CreateServiceAsync_CreatesApprovedServiceWithPriceRange()
    {
        await using var db = CreateDbContext();
        var sut = new AdminServiceCatalogManagementService(db);

        var result = await sut.CreateServiceAsync(
            new UpsertServiceRequest("Blanchisserie", "Linge et pressing", "shirt", PriceMinAmount: 2500, PriceMaxAmount: 4500),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Equal("Blanchisserie", result.Response.Name);
        Assert.Equal("Approved", result.Response.Status);
        Assert.True(result.Response.IsActive);
        Assert.Equal(2500, result.Response.PriceMinAmount);
        Assert.Equal(4500, result.Response.PriceMaxAmount);
    }

    [Fact]
    public async Task CreateServiceAsync_RejectsDuplicateNormalizedName()
    {
        await using var db = CreateDbContext();
        db.Services.Add(new Service("Ménage à domicile", null, createdByCompanyId: null));
        await db.SaveChangesAsync();

        var result = await new AdminServiceCatalogManagementService(db).CreateServiceAsync(
            new UpsertServiceRequest("Menage a domicile", null, "sparkles"),
            CancellationToken.None);

        Assert.Equal(AdminServiceCatalogOperationStatus.Conflict, result.Status);
        Assert.Single(await db.Services.ToListAsync());
    }

    [Fact]
    public async Task CreatePrestationAsync_UpdatesExistingPrestationInsteadOfDuplicating()
    {
        await using var db = CreateDbContext();
        var service = new Service("Jardinage", null, createdByCompanyId: null);
        service.AddPrestation("Tondre gazon", "Ancienne description", 1, 2000, 5000);
        db.Services.Add(service);
        await db.SaveChangesAsync();

        var result = await new AdminServiceCatalogManagementService(db).CreatePrestationAsync(
            service.Id,
            new UpsertServicePrestationRequest("tondre gazon", "Nouvelle description", 3, PriceMinAmount: 2500, PriceMaxAmount: 5500),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var saved = await db.Services.Include(item => item.Prestations).SingleAsync();
        var prestation = Assert.Single(saved.Prestations);
        Assert.Equal("tondre gazon", prestation.Name);
        Assert.Equal("Nouvelle description", prestation.Description);
        Assert.Equal(3, prestation.SortOrder);
        Assert.Equal(2500, prestation.PriceMinAmount);
        Assert.Equal(5500, prestation.PriceMaxAmount);
    }

    [Fact]
    public async Task DeactivateAndActivatePrestationAsync_TogglesAvailability()
    {
        await using var db = CreateDbContext();
        var service = new Service("Electricite", null, createdByCompanyId: null);
        var prestation = service.AddPrestation("Diagnostic panne", null, 1, 5000, 15000);
        db.Services.Add(service);
        await db.SaveChangesAsync();

        var sut = new AdminServiceCatalogManagementService(db);
        var deactivated = await sut.DeactivatePrestationAsync(prestation.Id, CancellationToken.None);
        var activated = await sut.ActivatePrestationAsync(prestation.Id, CancellationToken.None);

        Assert.True(deactivated.IsSuccess);
        Assert.True(activated.IsSuccess);
        Assert.True((await db.ServicePrestations.SingleAsync()).IsActive);
    }

    [Fact]
    public async Task UpdateServiceAsync_RejectsDuplicateName()
    {
        await using var db = CreateDbContext();
        var menage = new Service("Menage", null, createdByCompanyId: null);
        var jardinage = new Service("Jardinage", null, createdByCompanyId: null);
        db.Services.AddRange(menage, jardinage);
        await db.SaveChangesAsync();

        var result = await new AdminServiceCatalogManagementService(db).UpdateServiceAsync(
            jardinage.Id,
            new UpsertServiceRequest("Menage", null, "sparkles"),
            CancellationToken.None);

        Assert.Equal(AdminServiceCatalogOperationStatus.Conflict, result.Status);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
