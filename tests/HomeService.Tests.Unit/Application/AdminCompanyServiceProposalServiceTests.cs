using HomeService.Application.Admin;
using HomeService.Application.Auditing;
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
    public async Task CreateServiceAsync_WhenAuditActorIsProvided_CreatesAuditLog()
    {
        await using var db = CreateDbContext();
        var application = CreateApplication("Ivoire Catering Group", "Repassage");
        var proposal = new CompanyApplicationService(application.Id, "Repassage");
        db.CompanyApplications.Add(application);
        db.CompanyApplicationServices.Add(proposal);
        await db.SaveChangesAsync();

        var result = await new AdminCompanyServiceProposalService(db).CreateServiceAsync(
            proposal.Id,
            new CreateServiceFromCompanyServiceProposalRequest("Blanchisserie pressing", "Service de linge et pressing", "shirt", 2500, 4500, "XOF"),
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "tests", "proposal-service"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var log = Assert.Single(db.AuditLogEntries);
        Assert.Equal("AdminCompanyServiceProposalServiceCreated", log.Action);
        Assert.Equal(nameof(CompanyApplicationService), log.EntityType);
        Assert.Equal(proposal.Id, log.EntityId);
        Assert.Equal("proposal-service", log.CorrelationId);
        Assert.Contains("Repassage", log.BeforeJson);
        Assert.Contains("Blanchisserie pressing", log.AfterJson);
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

    [Fact]
    public async Task AttachAsync_WhenPrestationSelected_RattachesProposalAndAudits()
    {
        await using var db = CreateDbContext();
        var application = CreateApplication("Ivoire Catering Group", "Repassage");
        var proposal = new CompanyApplicationService(application.Id, "Repassage");
        var service = new Service("Blanchisserie", "Linge et pressing", createdByCompanyId: null);
        var prestation = service.AddPrestation("Repassage", null, 1, 2500, 4500);
        db.CompanyApplications.Add(application);
        db.CompanyApplicationServices.Add(proposal);
        db.Services.Add(service);
        await db.SaveChangesAsync();

        var result = await new AdminCompanyServiceProposalService(db).AttachAsync(
            proposal.Id,
            new AttachCompanyServiceProposalRequest(service.Id, prestation.Id),
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "tests", "proposal-attach"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(service.Id, proposal.MatchedServiceId);
        Assert.Equal(prestation.Id, proposal.MatchedServicePrestationId);
        var log = Assert.Single(db.AuditLogEntries);
        Assert.Equal("AdminCompanyServiceProposalAttached", log.Action);
        Assert.Equal(nameof(CompanyApplicationService), log.EntityType);
        Assert.Equal(proposal.Id, log.EntityId);
        Assert.Equal("proposal-attach", log.CorrelationId);
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
