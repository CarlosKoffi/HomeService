using HomeService.Application.CompanyPortal;
using HomeService.Application.Missions;
using HomeService.Application.Notifications;
using HomeService.Contracts.Missions;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class MissionAdditionalQuoteWorkflowServiceTests
{
    [Fact]
    public async Task AdditionalQuoteWorkflow_RequestSubmitAndPay_TracksNotificationsAndFinancials()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedStartedMissionAsync(db);
        var sut = CreateService(db);

        var request = await sut.RequestFromProviderAsync(
            scenario.Provider.Id,
            scenario.Mission.Id,
            new RequestMissionAdditionalQuoteRequest("Il faut remplacer un flexible.", "missions/flexible.jpg"),
            CancellationToken.None);

        Assert.True(request.IsSuccess);
        Assert.Equal("Requested", request.Response!.Status);
        Assert.Equal("Il faut remplacer un flexible.", request.Response.Reason);
        Assert.Equal("missions/flexible.jpg", request.Response.PhotoStoragePath);
        Assert.Contains(await db.CompanyPortalNotifications.ToListAsync(), notification =>
            notification.CompanyId == scenario.Company.Id
            && notification.Type == "MissionAdditionalQuoteRequested");

        var submit = await sut.SubmitByCompanyAsync(
            scenario.Company.Id,
            request.Response.Id,
            new SubmitMissionAdditionalQuoteRequest(4_500, "xof", "Flexible et main d'oeuvre complementaire."),
            CancellationToken.None);

        Assert.True(submit.IsSuccess);
        Assert.Equal("Submitted", submit.Response!.Status);
        Assert.Equal(4_500, submit.Response.Amount);
        Assert.Equal("XOF", submit.Response.Currency);
        Assert.Contains(await db.NotificationOutboxMessages.ToListAsync(), message =>
            message.Channel == NotificationChannel.MobilePush
            && message.RelatedEntityId == request.Response.Id
            && message.Recipient == "customer-device-token");

        var payment = await sut.PayByCustomerAsync(
            request.Response.Id,
            new PayMissionAdditionalQuoteRequest(scenario.Customer.PhoneNumber, "MM-ADDITIONAL-001"),
            CancellationToken.None);

        Assert.True(payment.IsSuccess);
        Assert.Equal("Paid", payment.Response!.Status);
        Assert.NotNull(payment.Response.PaidAt);

        var quote = await db.MissionAdditionalQuotes.SingleAsync();
        Assert.Equal(MissionAdditionalQuoteStatus.Paid, quote.Status);
        Assert.Equal("MM-ADDITIONAL-001", quote.PaymentReference);

        var milestone = await db.MissionPaymentMilestones.SingleAsync(item =>
            item.MissionId == scenario.Mission.Id
            && item.Trigger == MissionPaymentMilestoneTrigger.AdditionalQuote);
        Assert.Equal(MissionPaymentMilestoneStatus.Paid, milestone.Status);
        Assert.Equal(4_500, milestone.Amount);

        Assert.Contains(await db.MissionFinancialBreakdowns.ToListAsync(), line =>
            line.MissionId == scenario.Mission.Id
            && line.LineType == MissionFinancialLineType.AdditionalQuote
            && line.Amount == 4_500);
        Assert.Contains(await db.CompanyPortalNotifications.ToListAsync(), notification =>
            notification.CompanyId == scenario.Company.Id
            && notification.Type == "MissionAdditionalQuotePaid");
        Assert.Contains(await db.NotificationOutboxMessages.ToListAsync(), message =>
            message.Channel == NotificationChannel.MobilePush
            && message.RelatedEntityId == request.Response.Id
            && message.Recipient == "provider-device-token");
    }

    [Fact]
    public async Task RequestFromProvider_WhenMissionHasNotStarted_IsRejected()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedAcceptedMissionAsync(db, startMission: false);
        var sut = CreateService(db);

        var result = await sut.RequestFromProviderAsync(
            scenario.Provider.Id,
            scenario.Mission.Id,
            new RequestMissionAdditionalQuoteRequest("Besoin piece.", null),
            CancellationToken.None);

        Assert.Equal(MissionAdditionalQuoteWorkflowStatus.ValidationFailed, result.Status);
        Assert.Empty(await db.MissionAdditionalQuotes.ToListAsync());
    }

    private static MissionAdditionalQuoteWorkflowService CreateService(HomeServiceDbContext db)
    {
        return new MissionAdditionalQuoteWorkflowService(
            db,
            new CompanyPortalNotificationWriter(db),
            new MobilePushNotificationQueueService(db));
    }

    private static async Task<AdditionalQuoteScenario> SeedStartedMissionAsync(HomeServiceDbContext db)
        => await SeedAcceptedMissionAsync(db, startMission: true);

    private static async Task<AdditionalQuoteScenario> SeedAcceptedMissionAsync(HomeServiceDbContext db, bool startMission)
    {
        var company = new Company("Wele Services", "+2250701111111", "ops@wele.ci");
        company.Approve();
        var customer = new CustomerProfile("Aya", "Kone", "+2250700000000");
        var service = new Service("Plomberie", "Depannage eau", null);
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
        mission.SetServiceLocation("Cocody Angre", 5.348850m, -4.003150m);
        mission.AssignWithCompanyQuote(provider.Id, company.Id, 20_000, 25_000, null);
        mission.MarkProviderAccepted(provider.Id, company.Id);
        mission.ConfirmByCustomer(3_000, 0, 1_500, 0);
        if (startMission)
        {
            mission.Start(provider.Id, company.Id);
        }

        db.Companies.Add(company);
        db.Customers.Add(customer);
        db.Services.Add(service);
        db.Providers.Add(provider);
        db.Missions.Add(mission);
        db.MobileDeviceTokens.Add(new MobileDeviceToken(
            MobileDeviceOwnerType.Customer,
            customer.Id,
            MobileDevicePlatform.Android,
            "customer-device-token",
            "Telephone client"));
        db.MobileDeviceTokens.Add(new MobileDeviceToken(
            MobileDeviceOwnerType.Provider,
            provider.Id,
            MobileDevicePlatform.Android,
            "provider-device-token",
            "Telephone prestataire"));
        await db.SaveChangesAsync();

        return new AdditionalQuoteScenario(company, customer, provider, mission);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }

    private sealed record AdditionalQuoteScenario(
        Company Company,
        CustomerProfile Customer,
        ProviderProfile Provider,
        Mission Mission);
}
