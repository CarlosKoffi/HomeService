using HomeService.Application.Clients;
using HomeService.Application.CompanyPortal;
using HomeService.Application.Notifications;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Contracts.Clients;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class ClientMissionCompletionValidationServiceTests
{
    private static readonly Guid ServiceId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task ValidateAsync_WhenMissionIsCompleted_StoresReviewAndReleasesCompanyPayout()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedCompletedMissionAsync(db);
        db.MobileDeviceTokens.Add(new MobileDeviceToken(
            MobileDeviceOwnerType.Provider,
            scenario.Provider.Id,
            MobileDevicePlatform.Android,
            "provider-token",
            "Android terrain"));
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.ValidateAsync(
            scenario.Mission.Id,
            ValidRequest(scenario.Customer.PhoneNumber),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Equal(PaymentStatus.Paid.ToString(), result.Response.PaymentStatus);
        Assert.NotNull(result.Response.CustomerCompletionValidatedAt);
        Assert.NotNull(result.Response.CompanyPayoutReleasedAt);
        Assert.Equal(4, result.Response.OverallRating);
        Assert.Equal(17_000, result.Response.CompanyPayoutAmount);
        Assert.Equal(1, await db.MissionReviews.CountAsync());
        Assert.Equal(1, await db.CompanyPortalActivities.CountAsync());
        Assert.Equal(1, await db.CompanyPortalNotifications.CountAsync());
        var notification = await db.CompanyPortalNotifications.SingleAsync();
        Assert.Equal("MissionPaymentReleased", notification.Type);
        Assert.Equal(scenario.Company.Id, notification.CompanyId);
        Assert.Equal(1, await db.NotificationOutboxMessages.CountAsync());
        var push = await db.NotificationOutboxMessages.SingleAsync();
        Assert.Equal(NotificationChannel.MobilePush, push.Channel);
        Assert.Equal("provider-token", push.Recipient);
        var milestone = await db.MissionPaymentMilestones.SingleAsync();
        Assert.Equal(MissionPaymentMilestoneStatus.Paid, milestone.Status);
        Assert.Equal("PAYOUT-001", milestone.ExternalPaymentReference);
    }

    [Fact]
    public async Task ValidateAsync_WhenPhoneDoesNotMatchCustomer_IsForbidden()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedCompletedMissionAsync(db);
        var sut = CreateService(db);

        var result = await sut.ValidateAsync(
            scenario.Mission.Id,
            ValidRequest("+2250101010101"),
            CancellationToken.None);

        Assert.Equal(ClientMissionCompletionValidationStatus.Forbidden, result.Status);
        Assert.Null(scenario.Mission.CustomerCompletionValidatedAt);
        Assert.Equal(PaymentStatus.Authorized, scenario.Mission.PaymentStatus);
    }

    [Fact]
    public async Task ValidateAsync_WhenRatingIsInvalid_IsRejected()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedCompletedMissionAsync(db);
        var sut = CreateService(db);

        var result = await sut.ValidateAsync(
            scenario.Mission.Id,
            ValidRequest(scenario.Customer.PhoneNumber) with { QualityRating = 0 },
            CancellationToken.None);

        Assert.Equal(ClientMissionCompletionValidationStatus.ValidationFailed, result.Status);
        Assert.Empty(db.MissionReviews);
    }

    private static ValidateClientMissionCompletionRequest ValidRequest(string phoneNumber)
    {
        return new ValidateClientMissionCompletionRequest(
            phoneNumber,
            QualityRating: 5,
            PunctualityRating: 4,
            PolitenessRating: 4,
            CleanlinessRating: 4,
            "Service propre.",
            "PAYOUT-001");
    }

    private static async Task<CompletionScenario> SeedCompletedMissionAsync(HomeServiceDbContext db)
    {
        var customer = new CustomerProfile("Aya", "Kone", "+2250700000000");
        var company = new Company("wélé Services", "+2250701111111", "ops@wele.ci");
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

        var mission = new Mission(customer.Id, ServiceId, MissionMode.Instant, PaymentMethod.MobileMoney, null, 90);
        mission.SetServiceLocation("Cocody", 5.348850m, -4.003150m);
        mission.AssignWithCompanyQuote(provider.Id, company.Id, 20_000, 25_000, null);
        mission.MarkProviderAccepted(provider.Id, company.Id);
        mission.ConfirmByCustomer(3_000, 0, 1500);
        mission.Start(provider.Id, company.Id);
        mission.Complete(90);

        db.Customers.Add(customer);
        db.Companies.Add(company);
        db.Providers.Add(provider);
        db.Missions.Add(mission);
        var milestone = new MissionPaymentMilestone(
            mission.Id,
            MissionPaymentMilestoneTrigger.MissionCompleted,
            17_000,
            "XOF",
            "Mission terminee - paiement entreprise a liberer",
            20);
        db.MissionPaymentMilestones.Add(milestone);
        await db.SaveChangesAsync();
        return new CompletionScenario(customer, company, provider, mission);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }

    private static ClientMissionCompletionValidationService CreateService(HomeServiceDbContext db)
    {
        return new ClientMissionCompletionValidationService(
            db,
            new CompanyPortalNotificationWriter(db),
            new MobilePushNotificationQueueService(db));
    }

    private sealed record CompletionScenario(
        CustomerProfile Customer,
        Company Company,
        ProviderProfile Provider,
        Mission Mission);
}
