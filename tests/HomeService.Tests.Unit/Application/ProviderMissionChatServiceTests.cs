using HomeService.Application.Notifications;
using HomeService.Application.ProviderPortal;
using HomeService.Contracts.ProviderPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class ProviderMissionChatServiceTests
{
    [Fact]
    public async Task SendAsync_WhenAssignmentIsActive_CreatesConversationMessageAndQueuesCustomerPush()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedScenarioAsync(db);
        db.MobileDeviceTokens.Add(new MobileDeviceToken(
            MobileDeviceOwnerType.Customer,
            scenario.Customer.Id,
            MobileDevicePlatform.Android,
            "customer-token",
            "Client Android"));
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.SendAsync(
            scenario.Provider.Id,
            scenario.Assignment.Id,
            new SendProviderMissionMessageRequest("Pouvez-vous envoyer une photo de l'evier ?"),
            CancellationToken.None);

        Assert.Equal(ProviderMissionChatResultStatus.Created, result.Status);
        Assert.Equal(1, await db.MissionConversations.CountAsync());
        var message = await db.MissionMessages.SingleAsync();
        Assert.Equal(MissionMessageSenderType.Provider, message.SenderType);
        Assert.Equal("Pouvez-vous envoyer une photo de l'evier ?", message.Body);
        var push = await db.NotificationOutboxMessages.SingleAsync(item =>
            item.Channel == NotificationChannel.MobilePush
            && item.OwnerType == MobileDeviceOwnerType.Customer);
        Assert.Equal(NotificationChannel.MobilePush, push.Channel);
        Assert.Equal("customer-token", push.Recipient);
        Assert.Contains(scenario.Mission.MissionNumber, push.Subject);
    }

    [Fact]
    public async Task ListAsync_WhenConversationExists_ReturnsMessagesInChronologicalOrder()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedScenarioAsync(db);
        var conversation = new MissionConversation(
            scenario.Mission.Id,
            scenario.Provider.Id,
            scenario.Company.Id,
            scenario.Customer.Id);
        db.MissionConversations.Add(conversation);
        db.MissionMessages.Add(new MissionMessage(conversation.Id, MissionMessageSenderType.Customer, scenario.Customer.Id, "Bonjour", null, null));
        db.MissionMessages.Add(new MissionMessage(conversation.Id, MissionMessageSenderType.Provider, scenario.Provider.Id, "Je suis en route", null, null));
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.ListAsync(scenario.Provider.Id, scenario.Assignment.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(scenario.Mission.MissionNumber, result.ChatResponse!.MissionNumber);
        Assert.Equal("Plomberie", result.ChatResponse.MissionLabel);
        Assert.Equal(2, result.ChatResponse!.Messages.Count);
        Assert.Equal("Bonjour", result.ChatResponse.Messages[0].Body);
        Assert.Equal("Je suis en route", result.ChatResponse.Messages[1].Body);
    }

    [Fact]
    public async Task ListAsync_WhenConversationWasCreatedBeforeAssignment_SynchronizesMissionParticipants()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedScenarioAsync(db);
        var conversation = new MissionConversation(
            scenario.Mission.Id,
            providerId: null,
            companyId: null,
            scenario.Customer.Id);
        db.MissionConversations.Add(conversation);
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.ListAsync(scenario.Provider.Id, scenario.Assignment.Id, CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(scenario.Mission.Id, result.ChatResponse!.MissionId);
        Assert.Equal(scenario.Provider.Id, conversation.ProviderId);
        Assert.Equal(scenario.Company.Id, conversation.CompanyId);
    }

    [Fact]
    public async Task SendAsync_WhenMessageIsEmpty_IsRejected()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedScenarioAsync(db);
        var sut = CreateService(db);

        var result = await sut.SendAsync(
            scenario.Provider.Id,
            scenario.Assignment.Id,
            new SendProviderMissionMessageRequest("   "),
            CancellationToken.None);

        Assert.Equal(ProviderMissionChatResultStatus.Invalid, result.Status);
        Assert.Empty(db.MissionMessages);
    }

    [Fact]
    public async Task SendAsync_WhenAssignmentIsRefused_IsRejected()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedScenarioAsync(db);
        scenario.Assignment.Refuse(ProviderMissionRefusalReason.Unavailable, "Pas disponible.");
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.SendAsync(
            scenario.Provider.Id,
            scenario.Assignment.Id,
            new SendProviderMissionMessageRequest("Je peux finalement venir."),
            CancellationToken.None);

        Assert.Equal(ProviderMissionChatResultStatus.Invalid, result.Status);
        Assert.Empty(db.MissionMessages);
    }

    private static ProviderMissionChatService CreateService(HomeServiceDbContext db)
    {
        return new ProviderMissionChatService(
            db,
            new MobilePushNotificationQueueService(db),
            new MobileNavigationBadgeService(db));
    }

    private static async Task<ProviderMissionChatScenario> SeedScenarioAsync(HomeServiceDbContext db)
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
            description: "Fuite sous evier",
            requiresCompanyQuote: true);
        mission.AssignWithCompanyQuote(provider.Id, company.Id, 20_000, 25_000, null);

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

        return new ProviderMissionChatScenario(company, customer, provider, mission, assignment);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }

    private sealed record ProviderMissionChatScenario(
        Company Company,
        CustomerProfile Customer,
        ProviderProfile Provider,
        Mission Mission,
        ProviderMissionAssignment Assignment);
}
