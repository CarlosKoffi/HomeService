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
        provider.SetAvailability(true, 5.35m, -4.02m);
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
        db.MobileDeviceTokens.Add(new MobileDeviceToken(
            MobileDeviceOwnerType.Customer,
            customer.Id,
            MobileDevicePlatform.Android,
            "customer-token",
            "Telephone client"));
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
        Assert.DoesNotContain(await db.NotificationOutboxMessages.ToListAsync(), message => message.Recipient == "customer-token");
        Assert.Equal($"Mission {mission.MissionNumber}", notification.Subject);
        Assert.Equal("Malou Diallo, mission Jardinage pour CI Home Service.", notification.Body);
        Assert.Contains(result.Response!.AssignmentId.ToString(), notification.MetadataJson);
    }

    [Fact]
    public async Task AssignAsync_WhenProviderAlreadyRefusedMission_IsRejected()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedAssignableMissionAsync(db);
        db.ProviderMissionAssignments.Add(new ProviderMissionAssignment(
            scenario.Mission.Id,
            scenario.Provider.Id,
            scenario.Company.Id,
            DateTimeOffset.UtcNow.AddMinutes(3)));
        await db.SaveChangesAsync();
        var refusedAssignment = await db.ProviderMissionAssignments.SingleAsync();
        refusedAssignment.Refuse(ProviderMissionRefusalReason.Unavailable, "Pas disponible.");
        await db.SaveChangesAsync();

        var assignmentService = CreateAssignmentService(db);

        var result = await assignmentService.AssignAsync(
            scenario.Company.Id,
            scenario.Mission.Id,
            scenario.Provider.Id,
            quotedAmount: 8_000,
            overMaxJustification: null,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("deja refuse", result.Message);
    }

    [Fact]
    public async Task ListAssignableProvidersAsync_WhenMissionHasPrestation_ExplainsWhyOtherProviderCannotBeAssigned()
    {
        await using var db = CreateDbContext();
        var company = new Company("CI Home Service", "+2250700000000", "contact@cihome.ci");
        company.Approve();
        var customer = new CustomerProfile("Awa", "Kone", "+2250700000001");
        var service = new Service("Jardinage", "Entretien exterieur", null);
        service.UpdatePriceRange(5_000, 12_000, "XOF");
        var lawn = service.AddPrestation("Tondre le gazon", null, 1, 5_000, 8_000, "XOF");
        var hedge = service.AddPrestation("Tailler une haie", null, 2, 6_000, 10_000, "XOF");
        var matchingProvider = CreateProvider(company.Id, "Malou", "+2250700000002");
        var otherProvider = CreateProvider(company.Id, "Awa", "+2250700000003");
        matchingProvider.SyncCompanyServices([(service.Id, ExperienceLevel.Confirmed, 6, ProviderServicePriceTier.Normal)]);
        matchingProvider.Services.Single().SyncPrestations([lawn.Id]);
        otherProvider.SyncCompanyServices([(service.Id, ExperienceLevel.Confirmed, 4, ProviderServicePriceTier.Normal)]);
        otherProvider.Services.Single().SyncPrestations([hedge.Id]);
        matchingProvider.Approve();
        otherProvider.Approve();
        matchingProvider.SetAvailability(true, 5.35m, -4.02m);
        otherProvider.SetAvailability(true, 5.35m, -4.02m);
        var mission = new Mission(
            customer.Id,
            service.Id,
            MissionMode.Instant,
            PaymentMethod.MobileMoney,
            null,
            90,
            lawn.Id,
            "Tondre la pelouse",
            true);
        mission.StartCompanySearch();
        mission.MarkCompanyOffersSent();
        mission.AcceptCompanyOffer(company.Id, DateTimeOffset.UtcNow.AddMinutes(10));

        db.AddRange(company, customer, service, matchingProvider, otherProvider, mission);
        await db.SaveChangesAsync();

        var result = await CreateAssignmentService(db).ListAssignableProvidersAsync(company.Id, mission.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Providers.Count);
        var provider = Assert.Single(result.Providers, item => item.Id == matchingProvider.Id);
        Assert.True(provider.CanAssign);
        Assert.Null(provider.BlockingReason);
        var blockedProvider = Assert.Single(result.Providers, item => item.Id == otherProvider.Id);
        Assert.False(blockedProvider.CanAssign);
        Assert.Contains("service", blockedProvider.BlockingReason);
    }

    private static CompanyMissionAssignmentService CreateAssignmentService(HomeServiceDbContext db)
    {
        return new CompanyMissionAssignmentService(
            db,
            new MobilePushNotificationQueueService(db),
            new NotificationDeliveryPreferenceService(db),
            new NotificationTemplateService(db));
    }

    private static async Task<AssignableMissionScenario> SeedAssignableMissionAsync(HomeServiceDbContext db)
    {
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
        provider.SetAvailability(true, 5.35m, -4.02m);
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
        await db.SaveChangesAsync();

        return new AssignableMissionScenario(company, provider, mission);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }

    private static ProviderProfile CreateProvider(Guid companyId, string firstName, string phoneNumber)
    {
        return new ProviderProfile(
            companyId,
            firstName,
            "Diallo",
            phoneNumber,
            $"{firstName.ToLowerInvariant()}@example.ci",
            new DateOnly(1994, 5, 10),
            "Cocody",
            ProviderGender.Male,
            ProviderEmploymentType.CompanyEmployee,
            6,
            5.35m,
            -4.02m,
            5);
    }

    private sealed record AssignableMissionScenario(
        Company Company,
        ProviderProfile Provider,
        Mission Mission);
}
