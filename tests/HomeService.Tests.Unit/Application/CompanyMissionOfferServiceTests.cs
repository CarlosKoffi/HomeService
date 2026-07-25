using HomeService.Application.CompanyPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class CompanyMissionOfferServiceTests
{
    [Fact]
    public async Task AcceptAsync_WhenOfferIsOpen_AttachesMissionToCompanyAndClosesCompetingOffers()
    {
        await using var db = CreateDbContext();
        var service = new Service("Plomberie", null, createdByCompanyId: null);
        var customer = new CustomerProfile("Awa", "Kone", "+2250700000001");
        var winningCompany = ApprovedCompany("Winner", 1);
        var otherCompany = ApprovedCompany("Other", 2);
        var mission = new Mission(
            customer.Id,
            service.Id,
            MissionMode.Instant,
            PaymentMethod.MobileMoney,
            null,
            90,
            description: "Evier qui fuit",
            requiresCompanyQuote: true);
        mission.StartCompanySearch();
        mission.MarkCompanyOffersSent();

        var winningOffer = new MissionDispatchOffer(
            mission.Id,
            winningCompany.Id,
            rank: 1,
            score: 10,
            scoreDetails: "Priorite manuelle",
            DateTimeOffset.UtcNow.AddMinutes(5));
        var competingOffer = new MissionDispatchOffer(
            mission.Id,
            otherCompany.Id,
            rank: 2,
            score: 20,
            scoreDetails: "Deuxieme choix",
            DateTimeOffset.UtcNow.AddMinutes(5));

        db.Services.Add(service);
        db.Customers.Add(customer);
        db.Companies.AddRange(winningCompany, otherCompany);
        db.Missions.Add(mission);
        db.MissionDispatchOffers.AddRange(winningOffer, competingOffer);
        await db.SaveChangesAsync();

        var result = await new CompanyMissionOfferService(db).AcceptAsync(winningCompany.Id, winningOffer.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(MissionStatus.SearchingProvider.ToString(), result.Response!.Status);
        Assert.Equal(winningCompany.Id, mission.CompanyId);
        Assert.Equal(MissionDispatchOfferStatus.Accepted, winningOffer.Status);
        Assert.Equal(MissionDispatchOfferStatus.Lost, competingOffer.Status);
        Assert.Single(db.CompanyPortalActivities);
    }

    [Fact]
    public async Task AcceptAsync_WhenOfferIsExpired_DoesNotAttachMission()
    {
        await using var db = CreateDbContext();
        var service = new Service("Electricite", null, createdByCompanyId: null);
        var customer = new CustomerProfile("Awa", "Kone", "+2250700000001");
        var company = ApprovedCompany("Electricite CI", 1);
        var mission = new Mission(customer.Id, service.Id, MissionMode.Instant, PaymentMethod.Card, null, 60);
        mission.StartCompanySearch();
        mission.MarkCompanyOffersSent();
        var offer = new MissionDispatchOffer(
            mission.Id,
            company.Id,
            rank: 1,
            score: 10,
            scoreDetails: "Expire",
            DateTimeOffset.UtcNow.AddMinutes(-1));

        db.Services.Add(service);
        db.Customers.Add(customer);
        db.Companies.Add(company);
        db.Missions.Add(mission);
        db.MissionDispatchOffers.Add(offer);
        await db.SaveChangesAsync();

        var result = await new CompanyMissionOfferService(db).AcceptAsync(company.Id, offer.Id, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(MissionDispatchOfferStatus.Expired, offer.Status);
        Assert.Null(mission.CompanyId);
    }

    private static Company ApprovedCompany(string name, int priority)
    {
        var company = new Company(name, "+2250700000000", $"{name.Replace(" ", "").ToLowerInvariant()}@wele.ci");
        company.Approve();
        company.UpdateMissionDispatchSettings(priority, acceptsUrgentMissions: true);
        return company;
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
