using HomeService.Application.Admin;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminServiceCatalogInsightsServiceTests
{
    [Fact]
    public async Task GetAsync_FlagsServiceWithDemandButNoApprovedProviders()
    {
        await using var db = CreateDbContext();
        var service = new Service("Plomberie", null, createdByCompanyId: null);
        var customer = new CustomerProfile("Aya", "Kone", "+2250700000000");
        var mission = new Mission(customer.Id, service.Id, MissionMode.Instant, PaymentMethod.MobileMoney, null, 60);
        db.Services.Add(service);
        db.Customers.Add(customer);
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var result = await new AdminServiceCatalogInsightsService(db).GetAsync(CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.True(item.HasProviderGap);
        Assert.True(item.HasDemandWithoutProviders);
        Assert.Equal("Recruter des prestataires", item.RecommendedAction);
        Assert.Equal(1, result.Totals.ServicesWithoutProviders);
        Assert.Equal(1, result.Totals.ServicesWithDemandWithoutProviders);
    }

    [Fact]
    public async Task GetAsync_CountsPendingProposalsForMatchingServiceText()
    {
        await using var db = CreateDbContext();
        var service = new Service("Blanchisserie", null, createdByCompanyId: null);
        var application = new CompanyApplication(
            "Ivoire Catering Group",
            registrationNumber: null,
            city: "Abidjan",
            address: "Cocody",
            contactName: "Gerant Test",
            email: "gerant@example.ci",
            phoneNumber: "+2250700000000",
            plannedServices: "Repassage",
            estimatedProviderCount: 2);
        var proposal = new CompanyApplicationService(application.Id, "Blanchisserie - Repassage");
        db.Services.Add(service);
        db.CompanyApplications.Add(application);
        db.CompanyApplicationServices.Add(proposal);
        await db.SaveChangesAsync();

        var result = await new AdminServiceCatalogInsightsService(db).GetAsync(CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(1, item.PendingProposalCount);
        Assert.Equal(1, result.Totals.PendingProposalCount);
        Assert.Equal("Classer les propositions", item.RecommendedAction);
    }

    [Fact]
    public async Task GetAsync_CountsApprovedProvidersAndInterimProviders()
    {
        await using var db = CreateDbContext();
        var company = new Company("CI Home Service", "+2250700000000", "contact@example.ci");
        var service = new Service("Menage", null, createdByCompanyId: null);
        var provider = new ProviderProfile(
            company.Id,
            "Awa",
            "Konate",
            "+2250700000001",
            "awa@example.ci",
            new DateOnly(1995, 1, 1),
            "Cocody",
            ProviderGender.Female,
            ProviderEmploymentType.TemporaryWorker,
            4,
            null,
            null,
            5);
        provider.SyncCompanyServices([(service.Id, ExperienceLevel.Confirmed, 4, ProviderServicePriceTier.Normal)]);
        provider.Approve();
        db.Companies.Add(company);
        db.Services.Add(service);
        db.Providers.Add(provider);
        await db.SaveChangesAsync();

        var result = await new AdminServiceCatalogInsightsService(db).GetAsync(CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(1, item.CompanyCount);
        Assert.Equal(1, item.ActiveProviderCount);
        Assert.Equal(1, item.InterimProviderCount);
        Assert.False(item.HasProviderGap);
        Assert.Equal("Surveiller", item.RecommendedAction);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
