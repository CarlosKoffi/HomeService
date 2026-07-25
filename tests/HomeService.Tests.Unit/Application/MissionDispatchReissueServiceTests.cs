using HomeService.Application.Missions;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class MissionDispatchReissueServiceTests
{
    [Fact]
    public async Task ExpireAndReissueMissionOffersAsync_WhenOfferExpired_CreatesNextWaveExcludingPreviousCompanies()
    {
        await using var db = CreateDbContext();
        var service = new Service("Serrurerie", null, createdByCompanyId: null);
        var customer = new CustomerProfile("Awa", "Kone", "+2250700000001");
        var firstCompany = ApprovedCompany("Premiere", priority: 1);
        var secondCompany = ApprovedCompany("Deuxieme", priority: 2);

        var firstProvider = Provider(firstCompany.Id, service.Id, "Awa");
        var secondProvider = Provider(secondCompany.Id, service.Id, "Mamadou");
        var mission = new Mission(customer.Id, service.Id, MissionMode.Instant, PaymentMethod.MobileMoney, null, 90);
        mission.SetServiceLocation("Cocody Angre", null, null);
        mission.StartCompanySearch();
        mission.MarkCompanyOffersSent();
        var expiredOffer = new MissionDispatchOffer(
            mission.Id,
            firstCompany.Id,
            rank: 1,
            score: 10,
            scoreDetails: "Premier choix",
            DateTimeOffset.UtcNow.AddMinutes(-1));

        db.Services.Add(service);
        db.Customers.Add(customer);
        db.Companies.AddRange(firstCompany, secondCompany);
        db.Providers.AddRange(firstProvider, secondProvider);
        db.Missions.Add(mission);
        db.MissionDispatchOffers.Add(expiredOffer);
        await db.SaveChangesAsync();

        var sut = new MissionDispatchService(db, new MissionDispatchScoringService());

        var result = await sut.ExpireAndReissueMissionOffersAsync(
            mission.Id,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.ExpiredOfferCount);
        Assert.Equal(1, result.CreatedOfferCount);
        Assert.Equal(MissionDispatchOfferStatus.Expired, expiredOffer.Status);
        Assert.Equal(2, await db.MissionDispatchOffers.CountAsync());
        Assert.Contains(db.MissionDispatchOffers, offer =>
            offer.CompanyId == secondCompany.Id
            && offer.Status == MissionDispatchOfferStatus.Sent);
        Assert.DoesNotContain(db.MissionDispatchOffers, offer =>
            offer.CompanyId == firstCompany.Id
            && offer.Status == MissionDispatchOfferStatus.Sent);
    }

    [Fact]
    public async Task ExpireAndReissueMissionOffersAsync_WhenAcceptedOfferExists_DoesNotCreateNewWave()
    {
        await using var db = CreateDbContext();
        var service = new Service("Electricite", null, createdByCompanyId: null);
        var customer = new CustomerProfile("Awa", "Kone", "+2250700000001");
        var company = ApprovedCompany("Electricite CI", priority: 1);
        var mission = new Mission(customer.Id, service.Id, MissionMode.Instant, PaymentMethod.Card, null, 60);
        mission.StartCompanySearch();
        mission.MarkCompanyOffersSent();
        var offer = new MissionDispatchOffer(
            mission.Id,
            company.Id,
            rank: 1,
            score: 10,
            scoreDetails: "Acceptee",
            DateTimeOffset.UtcNow.AddMinutes(5));
        mission.AcceptCompanyOffer(company.Id, DateTimeOffset.UtcNow.AddMinutes(5));
        offer.Accept(DateTimeOffset.UtcNow);

        db.Services.Add(service);
        db.Customers.Add(customer);
        db.Companies.Add(company);
        db.Missions.Add(mission);
        db.MissionDispatchOffers.Add(offer);
        await db.SaveChangesAsync();

        var sut = new MissionDispatchService(db, new MissionDispatchScoringService());

        var result = await sut.ExpireAndReissueMissionOffersAsync(
            mission.Id,
            DateTimeOffset.UtcNow.AddMinutes(10),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.CreatedOfferCount);
        Assert.Single(db.MissionDispatchOffers);
    }

    [Fact]
    public async Task ExpireAndReissueMissionOffersAsync_WhenAcceptedCompanyDoesNotAssignProvider_ReissuesToAnotherCompany()
    {
        await using var db = CreateDbContext();
        var service = new Service("Plomberie", null, createdByCompanyId: null);
        var customer = new CustomerProfile("Awa", "Kone", "+2250700000001");
        var firstCompany = ApprovedCompany("Premiere plomberie", priority: 1);
        var secondCompany = ApprovedCompany("Deuxieme plomberie", priority: 2);
        var firstProvider = Provider(firstCompany.Id, service.Id, "Awa");
        var secondProvider = Provider(secondCompany.Id, service.Id, "Mamadou");
        var mission = new Mission(customer.Id, service.Id, MissionMode.Instant, PaymentMethod.MobileMoney, null, 90);
        mission.StartCompanySearch();
        mission.MarkCompanyOffersSent();
        var acceptedOffer = new MissionDispatchOffer(
            mission.Id,
            firstCompany.Id,
            rank: 1,
            score: 10,
            scoreDetails: "Premiere vague",
            DateTimeOffset.UtcNow.AddMinutes(5));
        var now = DateTimeOffset.UtcNow;
        mission.AcceptCompanyOffer(firstCompany.Id, now.AddMinutes(-1));
        acceptedOffer.Accept(now.AddMinutes(-10));

        db.Services.Add(service);
        db.Customers.Add(customer);
        db.Companies.AddRange(firstCompany, secondCompany);
        db.Providers.AddRange(firstProvider, secondProvider);
        db.Missions.Add(mission);
        db.MissionDispatchOffers.Add(acceptedOffer);
        await db.SaveChangesAsync();

        var sut = new MissionDispatchService(db, new MissionDispatchScoringService());

        var result = await sut.ExpireAndReissueMissionOffersAsync(mission.Id, now, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.ExpiredOfferCount);
        Assert.Equal(1, result.CreatedOfferCount);
        Assert.Null(mission.CompanyId);
        Assert.Null(mission.CompanyAssignmentExpiresAt);
        Assert.Equal(MissionStatus.Offered, mission.Status);
        Assert.Equal(MissionDispatchOfferStatus.AssignmentTimedOut, acceptedOffer.Status);
        Assert.Contains(db.MissionDispatchOffers, offer =>
            offer.CompanyId == secondCompany.Id
            && offer.Status == MissionDispatchOfferStatus.Sent);
    }

    private static Company ApprovedCompany(string name, int priority)
    {
        var company = new Company(name, "+2250700000000", $"{name.Replace(" ", "").ToLowerInvariant()}@wele.ci");
        company.Approve();
        company.UpdateMissionDispatchSettings(priority, acceptsUrgentMissions: true);
        return company;
    }

    private static ProviderProfile Provider(Guid companyId, Guid serviceId, string firstName)
    {
        var provider = new ProviderProfile(
            companyId,
            firstName,
            "Kone",
            $"+22507{Guid.NewGuid():N}"[..14],
            null,
            new DateOnly(1995, 1, 10),
            "Cocody",
            ProviderGender.Female,
            ProviderEmploymentType.CompanyEmployee,
            4,
            null,
            null,
            5);
        provider.Approve();
        provider.SyncCompanyServices([(serviceId, ExperienceLevel.Confirmed, 4, ProviderServicePriceTier.Normal)]);
        return provider;
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
