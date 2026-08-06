using HomeService.Application.CompanyPortal;
using HomeService.Application.Notifications;
using HomeService.Application.ProviderPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class ProviderMissionNotificationServiceTests
{
    [Fact]
    public async Task NotifyAcceptedAsync_QueuesCompanyPortalAndCustomerMobileNotifications()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedScenarioAsync(db);
        db.MobileDeviceTokens.Add(new MobileDeviceToken(
            MobileDeviceOwnerType.Customer,
            scenario.Customer.Id,
            MobileDevicePlatform.Android,
            "customer-token",
            "Pixel test"));
        db.NotificationTemplates.Add(new NotificationTemplate(
            "MissionPaymentRequired",
            NotificationTemplateChannel.MobilePush,
            "Paiement mission requis",
            "Customer",
            "Paiement requis pour {NumeroMission}",
            "{NomTechnicien} a accepte. Payez {Montant} pour lancer la mission.",
            NotificationTemplateCatalog.CommonVariables));
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await service.NotifyAcceptedAsync(scenario.Mission, scenario.Provider, scenario.Assignment, CancellationToken.None);
        await db.SaveChangesAsync();

        var portal = await db.CompanyPortalNotifications.SingleAsync();
        Assert.Equal(scenario.Company.Id, portal.CompanyId);
        Assert.Equal("MissionProviderAccepted", portal.Type);
        Assert.Contains(scenario.Provider.FullName, portal.Message);

        var push = await db.NotificationOutboxMessages.SingleAsync();
        Assert.Equal(NotificationChannel.MobilePush, push.Channel);
        Assert.Equal("customer-token", push.Recipient);
        Assert.Equal($"Paiement requis pour {scenario.Mission.MissionNumber}", push.Subject);
        Assert.Contains(scenario.Provider.FullName, push.Body);
        Assert.Contains("7", push.Body);
        Assert.Contains("mission_payment_required", push.MetadataJson);
    }

    [Fact]
    public async Task NotifyArrivedAsync_DoesNotNotifyCustomerWithoutRequiredAction()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedScenarioAsync(db);
        db.MobileDeviceTokens.Add(new MobileDeviceToken(
            MobileDeviceOwnerType.Customer,
            scenario.Customer.Id,
            MobileDevicePlatform.Ios,
            "ios-token",
            "iPhone test"));
        db.NotificationTemplates.Add(new NotificationTemplate(
            "MissionTechnicianArrived",
            NotificationTemplateChannel.MobilePush,
            "Technicien arrive",
            "Customer",
            "Arrivee {NumeroMission}",
            "{NomTechnicien} est bien sur place a {Adresse}.",
            NotificationTemplateCatalog.CommonVariables));
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await service.NotifyArrivedAsync(scenario.Mission, scenario.Provider, scenario.Assignment, CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.Empty(await db.NotificationOutboxMessages.ToListAsync());
    }

    private static ProviderMissionNotificationService CreateService(HomeServiceDbContext db)
    {
        return new ProviderMissionNotificationService(
            db,
            new CompanyPortalNotificationWriter(db),
            new MobilePushNotificationQueueService(db),
            new NotificationDeliveryPreferenceService(db),
            new NotificationTemplateService(db));
    }

    private static async Task<Scenario> SeedScenarioAsync(HomeServiceDbContext db)
    {
        var company = new Company("CI Home Service", "+2250700000000", "contact@cihome.ci");
        company.Approve();
        var customer = new CustomerProfile("Awa", "Kone", "+2250700000001");
        var service = new Service("Menage a domicile", "Nettoyage residentiel", null);
        service.UpdatePriceRange(4_000, 9_000, "XOF");
        var provider = new ProviderProfile(
            company.Id,
            "Malou",
            "Diallo",
            "+2250700000002",
            "malou@example.ci",
            new DateOnly(1994, 5, 10),
            "Cocody",
            ProviderGender.Male,
            ProviderEmploymentType.CompanyEmployee,
            6,
            5.35m,
            -4.02m,
            5);
        provider.Approve();
        var mission = new Mission(
            customer.Id,
            service.Id,
            MissionMode.Instant,
            PaymentMethod.MobileMoney,
            null,
            90,
            description: "Menage salon et cuisine",
            requiresCompanyQuote: true);
        mission.StartCompanySearch();
        mission.MarkCompanyOffersSent();
        mission.AcceptCompanyOffer(company.Id, DateTimeOffset.UtcNow.AddMinutes(10));
        mission.AssignWithCompanyQuote(provider.Id, company.Id, 7_000, 9_000, null);
        mission.SetServiceLocation("Cocody Angre", 5.348850m, -4.003150m, 250);
        var assignment = new ProviderMissionAssignment(
            mission.Id,
            provider.Id,
            company.Id,
            DateTimeOffset.UtcNow.AddMinutes(3));

        db.Companies.Add(company);
        db.Customers.Add(customer);
        db.Services.Add(service);
        db.Providers.Add(provider);
        db.Missions.Add(mission);
        db.ProviderMissionAssignments.Add(assignment);
        await db.SaveChangesAsync();

        return new Scenario(company, customer, provider, mission, assignment);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }

    private sealed record Scenario(
        Company Company,
        CustomerProfile Customer,
        ProviderProfile Provider,
        Mission Mission,
        ProviderMissionAssignment Assignment);
}
