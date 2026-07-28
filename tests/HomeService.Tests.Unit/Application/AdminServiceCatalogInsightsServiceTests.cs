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

    [Fact]
    public async Task GetAsync_CountsPrestationProvidersMissionsAndRevenue()
    {
        await using var db = CreateDbContext();
        var company = new Company("CI Home Service", "+2250700000000", "contact@example.ci");
        var service = new Service("Blanchisserie", null, createdByCompanyId: null);
        var repassage = service.AddPrestation("Repassage", null, 1, 2500, 4500);
        var provider = new ProviderProfile(
            company.Id,
            "Malou",
            "Diallo",
            "+2250700000002",
            "malou@example.ci",
            new DateOnly(1993, 5, 12),
            "Attecoube",
            ProviderGender.Male,
            ProviderEmploymentType.TemporaryWorker,
            6,
            null,
            null,
            5);
        provider.SyncCompanyServices([(service.Id, ExperienceLevel.Confirmed, 6, ProviderServicePriceTier.Normal)]);
        provider.Services.Single().SyncPrestations([repassage.Id]);
        provider.Approve();

        var customer = new CustomerProfile("Aya", "Kone", "+2250700000003");
        var mission = new Mission(
            customer.Id,
            service.Id,
            MissionMode.Instant,
            PaymentMethod.MobileMoney,
            null,
            60,
            repassage.Id,
            "Repassage a domicile",
            requiresCompanyQuote: true);
        mission.AssignWithCompanyQuote(provider.Id, company.Id, 4000, 4500, null);
        mission.MarkProviderAccepted(provider.Id, company.Id);
        mission.ConfirmByCustomer(platformCommissionAmount: 600, transportFeeAmount: 0, platformCommissionRateBasisPoints: 1500);
        mission.Start(provider.Id, company.Id);
        mission.Complete(60);
        mission.ValidateCompletionByCustomer();

        db.Companies.Add(company);
        db.Services.Add(service);
        db.Providers.Add(provider);
        db.Customers.Add(customer);
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var result = await new AdminServiceCatalogInsightsService(db).GetAsync(CancellationToken.None);

        var item = Assert.Single(result.Items);
        var prestation = Assert.Single(item.Prestations);
        Assert.Equal("Repassage", prestation.ServicePrestationName);
        Assert.Equal(1, prestation.ActiveProviderCount);
        Assert.Equal(1, prestation.MissionCount);
        Assert.Equal(1, prestation.CompletedMissionCount);
        Assert.Equal(4000, prestation.RevenueAmount);
        Assert.Equal(1, item.InterimProviderCount);
        Assert.Equal(4000, result.Totals.RevenueAmount);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
