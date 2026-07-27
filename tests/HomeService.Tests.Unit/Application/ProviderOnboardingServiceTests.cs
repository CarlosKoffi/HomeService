using HomeService.Application.ProviderPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class ProviderOnboardingServiceTests
{
    [Fact]
    public async Task SearchOptionsAsync_WhenLabelContainsParentServiceAndNewPrestation_ReturnsParentService()
    {
        await using var db = CreateDbContext();
        var service = new Service("Blanchisserie", "Linge, lavage et repassage", createdByCompanyId: null);
        db.Services.Add(service);
        await db.SaveChangesAsync();

        var options = await new ProviderOnboardingService(db).SearchOptionsAsync(
            "Blanchisserie - Repassage",
            CancellationToken.None);

        var option = Assert.Single(options);
        Assert.Equal("Service", option.Type);
        Assert.Equal(service.Id, option.ServiceId);
        Assert.Equal("Blanchisserie", option.ServiceName);
    }

    [Fact]
    public async Task SearchOpportunitiesAsync_WhenTypedLabelContainsParentService_ReturnsCompaniesForParentService()
    {
        await using var db = CreateDbContext();
        var service = new Service("Blanchisserie", "Linge, lavage et repassage", createdByCompanyId: null);
        var company = new Company("Ivoire Clean", "0700000000", "contact@ivoire-clean.ci");
        company.Approve();
        company.SetInterimApplications(true);
        company.UpdateCompanyInformation("Ivoire Clean", null, null, null, "Abidjan", "Cocody");
        db.Services.Add(service);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        db.ProviderServices.Add(new ProviderService(
            Guid.NewGuid(),
            company.Id,
            service.Id,
            ExperienceLevel.Confirmed,
            yearsOfExperience: 4));
        await db.SaveChangesAsync();

        var opportunities = await new ProviderOnboardingService(db).SearchOpportunitiesAsync(
            selectionType: null,
            selectionId: Guid.Empty,
            selectionLabel: "Blanchisserie - Repassage",
            address: "Cocody Angre",
            CancellationToken.None);

        var opportunity = Assert.Single(opportunities);
        Assert.Equal(company.Id, opportunity.CompanyId);
        Assert.Equal("Ivoire Clean", opportunity.CompanyName);
        Assert.Contains("Blanchisserie", opportunity.MatchingServices);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
