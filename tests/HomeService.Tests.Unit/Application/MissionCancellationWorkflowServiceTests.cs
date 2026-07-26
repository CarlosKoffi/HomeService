using HomeService.Application.CompanyPortal;
using HomeService.Application.Missions;
using HomeService.Application.Notifications;
using HomeService.Contracts.Missions;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class MissionCancellationWorkflowServiceTests
{
    [Fact]
    public async Task CancelAsync_WhenCompanyCancelsOwnMission_CancelsMissionAndAssignments()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedAcceptedMissionAsync(db);
        var assignment = new ProviderMissionAssignment(scenario.Mission.Id, scenario.Provider.Id, scenario.Company.Id, DateTimeOffset.UtcNow.AddMinutes(3));
        assignment.Accept();
        db.ProviderMissionAssignments.Add(assignment);
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.CancelAsync(
            scenario.Mission.Id,
            MissionCancellationActor.Company,
            new CancelMissionRequest("CompanyUnavailable", "Equipe indisponible", null),
            expectedCompanyId: scenario.Company.Id,
            expectedProviderId: null,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(MissionStatus.Cancelled, scenario.Mission.Status);
        Assert.Equal(MissionCancellationActor.Company, scenario.Mission.CancelledBy);
        Assert.Equal(MissionCancellationReason.CompanyUnavailable, scenario.Mission.CancellationReason);
        Assert.Equal(ProviderMissionAssignmentStatus.Cancelled, assignment.Status);
        Assert.Equal(2, await db.MissionPaymentMilestones.CountAsync());
        Assert.Equal(1, await db.CompanyPortalActivities.CountAsync());
        Assert.Equal(1, await db.CompanyPortalNotifications.CountAsync());
    }

    [Fact]
    public async Task CancelAsync_WhenCompanyDoesNotOwnMission_IsForbidden()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedAcceptedMissionAsync(db);
        var sut = CreateService(db);

        var result = await sut.CancelAsync(
            scenario.Mission.Id,
            MissionCancellationActor.Company,
            new CancelMissionRequest("CompanyUnavailable", "Pas pour nous", null),
            expectedCompanyId: Guid.NewGuid(),
            expectedProviderId: null,
            CancellationToken.None);

        Assert.Equal(MissionCancellationWorkflowStatus.Forbidden, result.Status);
        Assert.Equal(MissionStatus.Accepted, scenario.Mission.Status);
    }

    [Fact]
    public async Task CancelAsync_WhenProviderCancelsAssignedMission_TracksProviderActor()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedAcceptedMissionAsync(db);
        db.MobileDeviceTokens.Add(new MobileDeviceToken(
            MobileDeviceOwnerType.Provider,
            scenario.Provider.Id,
            MobileDevicePlatform.Android,
            "provider-device-token",
            "Android test"));
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.CancelAsync(
            scenario.Mission.Id,
            MissionCancellationActor.Provider,
            new CancelMissionRequest("ProviderUnavailable", "Probleme familial", 1500),
            expectedCompanyId: scenario.Company.Id,
            expectedProviderId: scenario.Provider.Id,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(MissionCancellationActor.Provider, scenario.Mission.CancelledBy);
        Assert.Equal(1500, scenario.Mission.CancellationFeeAmount);
        Assert.Equal(18_500, scenario.Mission.RefundAmount);
        Assert.Equal(1, await db.NotificationOutboxMessages.CountAsync());
        var push = await db.NotificationOutboxMessages.SingleAsync();
        Assert.Equal(NotificationChannel.MobilePush, push.Channel);
        Assert.Equal("provider-device-token", push.Recipient);
    }

    private static async Task<CancellationWorkflowScenario> SeedAcceptedMissionAsync(HomeServiceDbContext db)
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

        var mission = new Mission(customer.Id, service.Id, MissionMode.Instant, PaymentMethod.MobileMoney, null, 90, null, "Fuite", true);
        mission.AssignWithCompanyQuote(provider.Id, company.Id, 20_000, 25_000, null);
        mission.MarkProviderAccepted(provider.Id, company.Id);
        mission.AcceptCompanyQuote();
        mission.ConfirmByCustomer(3000, 0, 1500);
        db.Services.Add(service);
        db.Customers.Add(customer);
        db.Companies.Add(company);
        db.Providers.Add(provider);
        db.Missions.Add(mission);
        db.MissionPaymentMilestones.Add(new MissionPaymentMilestone(
            mission.Id,
            MissionPaymentMilestoneTrigger.QuoteAccepted,
            20_000,
            "XOF",
            "Paiement client bloque",
            0));
        await db.SaveChangesAsync();

        return new CancellationWorkflowScenario(customer, company, provider, mission);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }

    private static MissionCancellationWorkflowService CreateService(HomeServiceDbContext db)
    {
        return new MissionCancellationWorkflowService(
            db,
            new CompanyPortalNotificationWriter(db),
            new MobilePushNotificationQueueService(db));
    }

    private sealed record CancellationWorkflowScenario(
        CustomerProfile Customer,
        Company Company,
        ProviderProfile Provider,
        Mission Mission);
}
