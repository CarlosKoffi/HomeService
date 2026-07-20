using HomeService.Application.Admin;
using HomeService.Contracts.Services;
using HomeService.Domain.Entities;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminCompanyServiceProposalServiceTests
{
    [Fact]
    public async Task ListAsync_DoesNotReturnProposalAfterNewServiceCreation()
    {
        await using var db = CreateDbContext();
        var application = CreateApplication("Ivoire Catering Group", "Repassage");
        var proposal = new CompanyApplicationService(application.Id, "Repassage");
        db.CompanyApplications.Add(application);
        db.CompanyApplicationServices.Add(proposal);
        await db.SaveChangesAsync();

        var service = new AdminCompanyServiceProposalService(db);
        var result = await service.CreateServiceAsync(
            proposal.Id,
            new CreateServiceFromCompanyServiceProposalRequest("Blanchisserie pressing", "Service de linge et pressing", "shirt", 2500, 4500, "XOF"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var pending = await service.ListAsync(CancellationToken.None);
        Assert.Empty(pending.Items);
    }

    [Fact]
    public async Task ListAsync_StillReturnsUnmatchedProposal()
    {
        await using var db = CreateDbContext();
        var application = CreateApplication("Ivoire Catering Group", "Repassage");
        db.CompanyApplications.Add(application);
        db.CompanyApplicationServices.Add(new CompanyApplicationService(application.Id, "Repassage"));
        await db.SaveChangesAsync();

        var pending = await new AdminCompanyServiceProposalService(db).ListAsync(CancellationToken.None);

        var item = Assert.Single(pending.Items);
        Assert.Equal("Repassage", item.RawName);
    }

    [Fact]
    public async Task CreateServiceAsync_RattachesProposalToExistingServiceWhenNameAlreadyExists()
    {
        await using var db = CreateDbContext();
        var application = CreateApplication("Ivoire Catering Group", "Repassage");
        var proposal = new CompanyApplicationService(application.Id, "Repassage");
        var existingService = new Service("Blanchisserie", "Linge et pressing", createdByCompanyId: null);
        db.CompanyApplications.Add(application);
        db.CompanyApplicationServices.Add(proposal);
        db.Services.Add(existingService);
        await db.SaveChangesAsync();

        var result = await new AdminCompanyServiceProposalService(db).CreateServiceAsync(
            proposal.Id,
            new CreateServiceFromCompanyServiceProposalRequest("Blanchisserie"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(existingService.Id, proposal.MatchedServiceId);
        Assert.Single(await db.Services.ToListAsync());
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }

    private static CompanyApplication CreateApplication(string companyName, string plannedServices)
        => new(
            companyName,
            registrationNumber: null,
            city: "Abidjan",
            address: "Cocody",
            contactName: "Gerant Test",
            email: "gerant@example.ci",
            phoneNumber: "+2250700000000",
            plannedServices,
            estimatedProviderCount: 2);
}
