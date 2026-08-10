using HomeService.Application.Abstractions;
using HomeService.Application.Clients;
using HomeService.Application.CompanyPortal;
using HomeService.Application.Missions;
using HomeService.Application.Notifications;
using HomeService.Contracts.Clients;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class ClientMissionPaymentServiceTests
{
    [Fact]
    public async Task StartAsync_CreatesJekoRedirectWithGrossedUpProviderFee()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedAcceptedMissionAsync(db);
        var gateway = new FakeClientPaymentGateway();
        var sut = CreateService(db, gateway);

        var result = await sut.StartAsync(
            scenario.Customer.Id,
            scenario.Mission.Id,
            new StartClientMissionPaymentRequest(scenario.PaymentMethod.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Equal("Pending", result.Response.Status);
        Assert.Equal("orange", result.Response.ProviderCode);
        Assert.Equal(21_500, result.Response.ServiceAndPlatformAmount);
        Assert.Equal(328, result.Response.PaymentProviderFeeAmount);
        Assert.Equal(21_828, result.Response.TotalAmount);
        Assert.Equal("https://pay.jeko.africa/payment/test", result.Response.RedirectUrl);
        Assert.Equal(1, gateway.CreateCount);
        var stored = await db.MissionPaymentRequests.SingleAsync();
        Assert.Equal("jeko-payment-1", stored.ExternalPaymentRequestId);
        Assert.Null(scenario.Mission.CustomerConfirmedAt);
        Assert.False(scenario.Mission.CanRevealContactDetails);
    }

    [Fact]
    public async Task ApplyExternalStatusAsync_OnSignedSuccess_ConfirmsMissionOnlyOnce()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedAcceptedMissionAsync(db);
        var gateway = new FakeClientPaymentGateway();
        var sut = CreateService(db, gateway);
        var started = await sut.StartAsync(
            scenario.Customer.Id,
            scenario.Mission.Id,
            new StartClientMissionPaymentRequest(scenario.PaymentMethod.Id),
            CancellationToken.None);

        var applied = await sut.ApplyExternalStatusAsync(
            "jeko-payment-1",
            started.Response!.Reference,
            "success",
            null,
            "jeko-transaction-1",
            21_828,
            "XOF",
            CancellationToken.None);
        var appliedAgain = await sut.ApplyExternalStatusAsync(
            "jeko-payment-1",
            started.Response.Reference,
            "success",
            null,
            "jeko-transaction-1",
            21_828,
            "XOF",
            CancellationToken.None);

        Assert.True(applied);
        Assert.True(appliedAgain);
        Assert.Equal(MissionPaymentRequestStatus.Success, (await db.MissionPaymentRequests.SingleAsync()).Status);
        Assert.Equal(PaymentStatus.Authorized, scenario.Mission.PaymentStatus);
        Assert.NotNull(scenario.Mission.CustomerConfirmedAt);
        Assert.True(scenario.Mission.CanRevealContactDetails);
        Assert.Single(await db.MissionPaymentMilestones.ToListAsync());
    }

    [Fact]
    public async Task ApplyExternalStatusAsync_OnMismatchingAmount_DoesNotConfirmMission()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedAcceptedMissionAsync(db);
        var gateway = new FakeClientPaymentGateway();
        var sut = CreateService(db, gateway);
        var started = await sut.StartAsync(
            scenario.Customer.Id,
            scenario.Mission.Id,
            new StartClientMissionPaymentRequest(scenario.PaymentMethod.Id),
            CancellationToken.None);

        var applied = await sut.ApplyExternalStatusAsync(
            "jeko-payment-1",
            started.Response!.Reference,
            "success",
            null,
            "jeko-transaction-wrong-amount",
            20_000,
            "XOF",
            CancellationToken.None);

        Assert.True(applied);
        var payment = await db.MissionPaymentRequests.SingleAsync();
        Assert.Equal(MissionPaymentRequestStatus.Error, payment.Status);
        Assert.Contains("incoherent", payment.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Null(scenario.Mission.CustomerConfirmedAt);
        Assert.Empty(await db.MissionPaymentMilestones.ToListAsync());
    }

    [Fact]
    public async Task StartAsync_WhenPendingRequestExists_DoesNotCreateSecondDebit()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedAcceptedMissionAsync(db);
        var gateway = new FakeClientPaymentGateway();
        var sut = CreateService(db, gateway);

        var first = await sut.StartAsync(
            scenario.Customer.Id,
            scenario.Mission.Id,
            new StartClientMissionPaymentRequest(scenario.PaymentMethod.Id),
            CancellationToken.None);
        var second = await sut.StartAsync(
            scenario.Customer.Id,
            scenario.Mission.Id,
            new StartClientMissionPaymentRequest(scenario.PaymentMethod.Id),
            CancellationToken.None);

        Assert.Equal(first.Response!.Id, second.Response!.Id);
        Assert.Equal(first.Response.Reference, second.Response.Reference);
        Assert.Equal(1, gateway.CreateCount);
        Assert.Single(await db.MissionPaymentRequests.ToListAsync());
    }

    private static ClientMissionPaymentService CreateService(HomeServiceDbContext db, IClientPaymentGateway gateway)
    {
        var confirmation = new ClientMissionConfirmationService(
            db,
            new CompanyPortalNotificationWriter(db),
            new MobilePushNotificationQueueService(db),
            new MissionCommercialPricingService(db),
            gateway);
        return new ClientMissionPaymentService(
            db,
            gateway,
            new MissionCommercialPricingService(db),
            confirmation);
    }

    private static async Task<PaymentScenario> SeedAcceptedMissionAsync(HomeServiceDbContext db)
    {
        var service = new Service("Plomberie", null, null);
        var customer = new CustomerProfile("Aya", "Kone", "+2250700000000");
        var company = new Company("Entreprise plomberie", "+2250701111111", "ops@example.test");
        company.Approve();
        var provider = new ProviderProfile(
            company.Id,
            "Awa",
            "Konate",
            "+2250702222222",
            "awa@example.test",
            new DateOnly(1994, 2, 3),
            "Cocody",
            ProviderGender.Female,
            ProviderEmploymentType.CompanyEmployee,
            5,
            5.348850m,
            -4.003150m,
            5);
        provider.Approve();
        var paymentProvider = new PaymentProvider("orange-money", "Orange Money", PaymentMethod.MobileMoney, null, null, 1);
        var paymentMethod = new CustomerPaymentMethod(
            customer.Id,
            paymentProvider.Id,
            PaymentMethod.MobileMoney,
            "Mobile Money",
            "**** 0000",
            true);
        var mission = new Mission(
            customer.Id,
            service.Id,
            MissionMode.Instant,
            PaymentMethod.MobileMoney,
            null,
            60,
            requiresCompanyQuote: true);
        mission.SelectCustomerPaymentMethod(paymentMethod);
        mission.AssignWithCompanyQuote(provider.Id, company.Id, 20_000, 25_000, null);
        mission.MarkProviderAccepted(provider.Id, company.Id, DateTimeOffset.UtcNow.AddMinutes(30));

        db.AddRange(service, customer, company, provider, paymentProvider, paymentMethod, mission);
        await db.SaveChangesAsync();
        return new PaymentScenario(customer, mission, paymentMethod);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase($"jeko-payment-{Guid.NewGuid():N}")
            .Options;
        return new HomeServiceDbContext(options);
    }

    private sealed record PaymentScenario(
        CustomerProfile Customer,
        Mission Mission,
        CustomerPaymentMethod PaymentMethod);

    private sealed class FakeClientPaymentGateway : IClientPaymentGateway
    {
        public bool IsEnabled => true;
        public int FeeRateBasisPoints => 150;
        public int CreateCount { get; private set; }

        public Task<ClientPaymentGatewayResult> CreateAsync(
            ClientPaymentGatewayRequest request,
            CancellationToken cancellationToken)
        {
            CreateCount++;
            return Task.FromResult(new ClientPaymentGatewayResult(
                true,
                false,
                "pending",
                "jeko-payment-1",
                null,
                "https://pay.jeko.africa/payment/test",
                null,
                DateTimeOffset.UtcNow.AddMinutes(5)));
        }

        public Task<ClientPaymentGatewayResult> GetStatusAsync(
            string externalPaymentRequestId,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ClientPaymentGatewayResult(
                true,
                false,
                "pending",
                externalPaymentRequestId,
                null,
                "https://pay.jeko.africa/payment/test",
                null,
                DateTimeOffset.UtcNow.AddMinutes(5)));
    }
}
