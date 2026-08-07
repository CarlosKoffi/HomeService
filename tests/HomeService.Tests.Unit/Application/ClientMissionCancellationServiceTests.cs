using HomeService.Application.Clients;
using HomeService.Contracts.Clients;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class ClientMissionCancellationServiceTests
{
    [Fact]
    public async Task CancelAsync_BeforeContactRelease_CancelsWithoutFee()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedAssignedMissionAsync(db);
        var sut = new ClientMissionCancellationService(db);

        var result = await sut.CancelAsync(
            scenario.Mission.Id,
            new CancelClientMissionRequest(scenario.Customer.PhoneNumber, "CustomerChangedMind", "Plus besoin"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(MissionStatus.Cancelled, scenario.Mission.Status);
        Assert.Equal(0, scenario.Mission.CancellationFeeAmount);
        Assert.Equal(0, scenario.Mission.RefundAmount);
        Assert.Equal(MissionCancellationActor.Customer, scenario.Mission.CancelledBy);
        Assert.Equal(MissionCancellationReason.CustomerChangedMind, scenario.Mission.CancellationReason);
        Assert.Equal("Plus besoin", scenario.Mission.CancellationComment);
        Assert.Equal(ProviderMissionAssignmentStatus.Cancelled, scenario.Assignment.Status);
        Assert.True(scenario.Provider.IsAvailable);
        Assert.Single(await db.MissionPaymentMilestones.ToListAsync());
        Assert.Empty(await db.MissionFinancialBreakdowns.ToListAsync());
        Assert.Contains(
            await db.CompanyPortalNotifications.ToListAsync(),
            notification => notification.Title == "Mission annulée par le client"
                && notification.Message.Contains("Plus besoin", StringComparison.Ordinal));

        var recipients = await db.NotificationOutboxMessages
            .Select(notification => notification.OwnerType)
            .ToListAsync();
        Assert.Contains(MobileDeviceOwnerType.Customer, recipients);
        Assert.Contains(MobileDeviceOwnerType.Company, recipients);
        Assert.Contains(MobileDeviceOwnerType.Provider, recipients);
    }

    [Fact]
    public async Task CancelAsync_AfterContactRelease_KeepsFeeAndTracksRefund()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedAcceptedAndConfirmedMissionAsync(db);
        var sut = new ClientMissionCancellationService(db);

        var result = await sut.CancelAsync(
            scenario.Mission.Id,
            new CancelClientMissionRequest(scenario.Customer.PhoneNumber, "CustomerUnavailable", "Client indisponible"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(MissionStatus.Cancelled, scenario.Mission.Status);
        Assert.Equal(PaymentStatus.Refunded, scenario.Mission.PaymentStatus);
        Assert.Equal(2500, scenario.Mission.CancellationFeeAmount);
        Assert.Equal(17_500, scenario.Mission.RefundAmount);
        Assert.Equal(ProviderMissionAssignmentStatus.Cancelled, scenario.Assignment.Status);
        Assert.True(scenario.Provider.IsAvailable);
        Assert.Equal(2, await db.MissionPaymentMilestones.CountAsync());
        Assert.Equal(2, await db.MissionFinancialBreakdowns.CountAsync());
        Assert.Equal(1, await db.CompanyPortalActivities.CountAsync());
    }

    [Fact]
    public async Task CancelAsync_WhenMissionCompleted_IsRejected()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedAcceptedAndConfirmedMissionAsync(db);
        scenario.Mission.Start(scenario.Provider.Id, scenario.Company.Id);
        scenario.Mission.Complete(60);
        await db.SaveChangesAsync();
        var sut = new ClientMissionCancellationService(db);

        var result = await sut.CancelAsync(
            scenario.Mission.Id,
            new CancelClientMissionRequest(scenario.Customer.PhoneNumber, "CustomerChangedMind", null),
            CancellationToken.None);

        Assert.Equal(ClientMissionCancellationStatus.Invalid, result.Status);
        Assert.Equal(MissionStatus.Completed, scenario.Mission.Status);
    }

    private static async Task<CancellationScenario> SeedAssignedMissionAsync(HomeServiceDbContext db)
    {
        var service = new Service("Plomberie", null, createdByCompanyId: null);
        var customer = new CustomerProfile("Aya", "Kone", "+2250700000000");
        var company = new Company("wele Services", "+2250701111111", "ops@wele.ci");
        company.Approve();
        var provider = new ProviderProfile(
            company.Id,
            "Awa",
            "Konate",
            "+2250702222222",
            "awa@wele.ci",
            new DateOnly(1994, 2, 3),
            "Cocody",
            ProviderGender.Female,
            ProviderEmploymentType.CompanyEmployee,
            5,
            5.348850m,
            -4.003150m,
            5);
        provider.Approve();

        var mission = new Mission(
            customer.Id,
            service.Id,
            MissionMode.Instant,
            PaymentMethod.MobileMoney,
            null,
            90,
            null,
            "Fuite sous evier",
            requiresCompanyQuote: true);
        mission.AssignWithCompanyQuote(provider.Id, company.Id, 20_000, 25_000, null);
        var assignment = new ProviderMissionAssignment(
            mission.Id,
            provider.Id,
            company.Id,
            DateTimeOffset.UtcNow.AddMinutes(20));
        assignment.Accept();
        provider.SetAvailability(false, provider.CurrentLatitude, provider.CurrentLongitude);

        db.Services.Add(service);
        db.Customers.Add(customer);
        db.Companies.Add(company);
        db.Providers.Add(provider);
        db.Missions.Add(mission);
        db.ProviderMissionAssignments.Add(assignment);
        await db.SaveChangesAsync();

        return new CancellationScenario(customer, company, provider, mission, assignment);
    }

    private static async Task<CancellationScenario> SeedAcceptedAndConfirmedMissionAsync(HomeServiceDbContext db)
    {
        var scenario = await SeedAssignedMissionAsync(db);
        scenario.Mission.MarkProviderAccepted(scenario.Provider.Id, scenario.Company.Id);
        scenario.Mission.AcceptCompanyQuote();
        scenario.Mission.ConfirmByCustomer(3000, 0, 1500);
        db.MissionPaymentMilestones.Add(new MissionPaymentMilestone(
            scenario.Mission.Id,
            MissionPaymentMilestoneTrigger.QuoteAccepted,
            20_000,
            "XOF",
            "Paiement client bloque a l'acceptation du devis",
            0));
        await db.SaveChangesAsync();

        return scenario;
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }

    private sealed record CancellationScenario(
        CustomerProfile Customer,
        Company Company,
        ProviderProfile Provider,
        Mission Mission,
        ProviderMissionAssignment Assignment);
}
