using HomeService.Application.CompanyPortal;
using HomeService.Application.Notifications;
using HomeService.Contracts.CompanyPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class CompanyMissionAssignmentNotificationTests
{
    [Fact]
    public async Task AssignAsync_WhenProviderHasMobileToken_QueuesTemplatedMobilePush()
    {
        await using var db = CreateDbContext();
        var company = new Company("CI Home Service", "+2250700000000", "contact@cihome.ci");
        company.Approve();
        var customer = new CustomerProfile("Awa", "Kone", "+2250700000001");
        var service = new Service("Jardinage", "Entretien exterieur", null);
        service.UpdatePriceRange(5_000, 12_000, "XOF");
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
        provider.SyncCompanyServices([(service.Id, ExperienceLevel.Confirmed, 6, ProviderServicePriceTier.Normal)]);
        var mission = new Mission(
            customer.Id,
            service.Id,
            MissionMode.Instant,
            PaymentMethod.MobileMoney,
            null,
            90,
            description: "Taille de jardin",
            requiresCompanyQuote: true);
        mission.StartCompanySearch();
        mission.MarkCompanyOffersSent();
        mission.AcceptCompanyOffer(company.Id, DateTimeOffset.UtcNow.AddMinutes(10));

        db.Companies.Add(company);
        db.Customers.Add(customer);
        db.Services.Add(service);
        db.Providers.Add(provider);
        db.Missions.Add(mission);
        db.MobileDeviceTokens.Add(new MobileDeviceToken(
            MobileDeviceOwnerType.Provider,
            provider.Id,
            MobileDevicePlatform.Android,
            "provider-token",
            "Samsung test"));
        db.NotificationDeliveryRules.Add(new NotificationDeliveryRule(
            "MissionAssignedToProvider",
            "Mission affectee au prestataire",
            "Provider",
            portalEnabled: false,
            mobileAppEnabled: true,
            emailEnabled: false,
            whatsAppEnabled: false,
            subjectTemplate: "Mission {NumeroMission}",
            bodyTemplate: "{NomPrestataire}, mission {Service} pour {NomEntreprise}."));
        db.NotificationTemplates.Add(new NotificationTemplate(
            "MissionAssignedToProvider",
            NotificationTemplateChannel.MobilePush,
            "Mission affectee au prestataire",
            "Provider",
            "Mission {NumeroMission}",
            "{NomPrestataire}, mission {Service} pour {NomEntreprise}.",
            NotificationTemplateCatalog.CommonVariables));
        await db.SaveChangesAsync();

        var assignmentService = new CompanyMissionAssignmentService(
            db,
            new MobilePushNotificationQueueService(db),
            new NotificationDeliveryPreferenceService(db),
            new NotificationTemplateService(db));

        var result = await assignmentService.AssignAsync(
            company.Id,
            mission.Id,
            provider.Id,
            quotedAmount: 8_000,
            overMaxJustification: null,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var notification = await db.NotificationOutboxMessages.SingleAsync();
        Assert.Equal(NotificationChannel.MobilePush, notification.Channel);
        Assert.Equal("provider-token", notification.Recipient);
        Assert.Equal($"Mission {mission.MissionNumber}", notification.Subject);
        Assert.Equal("Malou Diallo, mission Jardinage pour CI Home Service.", notification.Body);
        Assert.Contains(result.Response!.AssignmentId.ToString(), notification.MetadataJson);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
