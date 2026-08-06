using HomeService.Application.Clients;
using HomeService.Application.CompanyPortal;
using HomeService.Application.Notifications;
using HomeService.Contracts.Clients;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class ClientMissionConfirmationServiceTests
{
    [Fact]
    public async Task ConfirmAsync_WhenProviderAcceptedMission_AuthorizesPaymentAndReleasesContacts()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedAcceptedMissionAsync(db);
        db.CommissionRules.Add(new CommissionRule("Commission wélé", CommissionRuleTarget.PlatformConnection, 1500, 0, "XOF"));
        db.MobileDeviceTokens.Add(new MobileDeviceToken(
            MobileDeviceOwnerType.Provider,
            scenario.Provider.Id,
            MobileDevicePlatform.Android,
            "provider-token",
            "Android terrain"));
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.ConfirmAsync(
            scenario.Mission.Id,
            new ConfirmClientMissionRequest(scenario.Customer.PhoneNumber, "MM-001"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Equal(PaymentStatus.Authorized.ToString(), result.Response.PaymentStatus);
        Assert.Equal(20_000, result.Response.TotalAmount);
        Assert.Equal(3_000, result.Response.PlatformCommissionAmount);
        Assert.Equal(17_000, result.Response.CompanyPayoutAmount);
        Assert.True(result.Response.ContactDetailsReleased);
        Assert.Equal(scenario.Company.PhoneNumber, result.Response.CompanyPhoneNumber);
        Assert.Equal(scenario.Provider.PhoneNumber, result.Response.ProviderPhoneNumber);
        Assert.Equal(1, await db.MissionPaymentMilestones.CountAsync());
        Assert.Equal(1, await db.CompanyPortalActivities.CountAsync());
        Assert.Equal(1, await db.CompanyPortalNotifications.CountAsync());
        var notification = await db.CompanyPortalNotifications.SingleAsync();
        Assert.Equal("MissionQuoteAcceptedByCustomer", notification.Type);
        Assert.Equal(scenario.Company.Id, notification.CompanyId);
        Assert.Equal(1, await db.NotificationOutboxMessages.CountAsync());
        var push = await db.NotificationOutboxMessages.SingleAsync();
        Assert.Equal(NotificationChannel.MobilePush, push.Channel);
        Assert.Equal("provider-token", push.Recipient);
    }

    [Fact]
    public async Task ConfirmAsync_WhenPhoneDoesNotMatchCustomer_IsForbidden()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedAcceptedMissionAsync(db);
        var sut = CreateService(db);

        var result = await sut.ConfirmAsync(
            scenario.Mission.Id,
            new ConfirmClientMissionRequest("+2250101010101", null),
            CancellationToken.None);

        Assert.Equal(ClientMissionConfirmationStatus.Forbidden, result.Status);
        Assert.Null(scenario.Mission.CustomerConfirmedAt);
        Assert.False(scenario.Mission.CanRevealContactDetails);
    }

    [Fact]
    public async Task ConfirmAsync_WhenMissionHasNoAcceptedProvider_IsRejected()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedAcceptedMissionAsync(db, markProviderAccepted: false);
        var sut = CreateService(db);

        var result = await sut.ConfirmAsync(
            scenario.Mission.Id,
            new ConfirmClientMissionRequest(scenario.Customer.PhoneNumber, null),
            CancellationToken.None);

        Assert.Equal(ClientMissionConfirmationStatus.Invalid, result.Status);
        Assert.Contains("prestataire", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(scenario.Mission.CustomerConfirmedAt);
    }

    [Fact]
    public async Task AutoConfirmExpiredAsync_WhenSavedPaymentMethodExists_ConfirmsMissionAndClearsDeadline()
    {
        await using var db = CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var scenario = await SeedAcceptedMissionAsync(db, paymentExpiresAt: now.AddSeconds(-1));
        var sut = CreateService(db);

        var confirmedCount = await sut.AutoConfirmExpiredAsync(now, 20, CancellationToken.None);

        Assert.Equal(1, confirmedCount);
        Assert.NotNull(scenario.Mission.CustomerConfirmedAt);
        Assert.Null(scenario.Mission.CustomerPaymentExpiresAt);
        Assert.Equal(PaymentStatus.Authorized, scenario.Mission.PaymentStatus);
        var milestone = await db.MissionPaymentMilestones.SingleAsync();
        Assert.Equal($"AUTO-CLIENT-DEADLINE-{scenario.Mission.Id:N}", milestone.ExternalPaymentReference);
    }

    private static async Task<ConfirmationScenario> SeedAcceptedMissionAsync(
        HomeServiceDbContext db,
        bool markProviderAccepted = true,
        DateTimeOffset? paymentExpiresAt = null)
    {
        var service = new Service("Plomberie", null, createdByCompanyId: null);
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
        var paymentMethod = new CustomerPaymentMethod(
            customer.Id,
            PaymentMethod.MobileMoney,
            "Orange Money",
            "**** 0000",
            isDefault: true);
        mission.SelectCustomerPaymentMethod(paymentMethod);
        mission.SetServiceLocation("Cocody", 5.348850m, -4.003150m);
        mission.AssignWithCompanyQuote(provider.Id, company.Id, 20_000, 25_000, null);
        if (markProviderAccepted)
        {
            mission.MarkProviderAccepted(provider.Id, company.Id, paymentExpiresAt);
        }

        db.Services.Add(service);
        db.Customers.Add(customer);
        db.CustomerPaymentMethods.Add(paymentMethod);
        db.Companies.Add(company);
        db.Providers.Add(provider);
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        return new ConfirmationScenario(customer, company, provider, mission);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }

    private static ClientMissionConfirmationService CreateService(HomeServiceDbContext db)
    {
        return new ClientMissionConfirmationService(
            db,
            new CompanyPortalNotificationWriter(db),
            new MobilePushNotificationQueueService(db));
    }

    private sealed record ConfirmationScenario(
        CustomerProfile Customer,
        Company Company,
        ProviderProfile Provider,
        Mission Mission);
}
