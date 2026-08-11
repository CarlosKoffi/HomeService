using HomeService.Application.Notifications;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class MobileNavigationBadgeServiceTests
{
    [Fact]
    public async Task UnreadMessages_AreDeduplicatedAcrossChannels_AndMarkedReadOnOpen()
    {
        await using var db = CreateDbContext();
        var missionId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var conversation = new MissionConversation(missionId, providerId, companyId, customerId);
        var receivedMessage = new MissionMessage(
            conversation.Id,
            MissionMessageSenderType.Provider,
            providerId,
            "Je suis en route.",
            null,
            null);
        var ownMessage = new MissionMessage(
            conversation.Id,
            MissionMessageSenderType.Customer,
            customerId,
            "Merci.",
            null,
            null);
        var metadata = $$"""{"messageId":"{{receivedMessage.Id:D}}"}""";
        db.MissionConversations.Add(conversation);
        db.MissionMessages.AddRange(receivedMessage, ownMessage);
        db.NotificationOutboxMessages.AddRange(
            CreateNotification(NotificationChannel.MobilePush, customerId, conversation.Id, metadata),
            CreateNotification(NotificationChannel.InApp, customerId, conversation.Id, metadata));
        await db.SaveChangesAsync();
        var sut = new MobileNavigationBadgeService(db);

        var before = await sut.GetUnreadMessageCountsByMissionAsync(
            MobileDeviceOwnerType.Customer,
            customerId,
            CancellationToken.None);
        await sut.MarkConversationMessagesReadAsync(
            MobileDeviceOwnerType.Customer,
            customerId,
            conversation.Id,
            CancellationToken.None);
        var after = await sut.GetUnreadMessageCountsByMissionAsync(
            MobileDeviceOwnerType.Customer,
            customerId,
            CancellationToken.None);

        Assert.Equal(1, before[missionId]);
        Assert.Empty(after);
        Assert.NotNull(receivedMessage.ReadAt);
        Assert.Null(ownMessage.ReadAt);
        Assert.All(await db.NotificationOutboxMessages.ToListAsync(), item => Assert.NotNull(item.ReadAt));
    }

    private static NotificationOutboxMessage CreateNotification(
        NotificationChannel channel,
        Guid customerId,
        Guid conversationId,
        string metadata)
        => new(
            channel,
            channel == NotificationChannel.MobilePush ? "device-token" : customerId.ToString("D"),
            "Nouveau message",
            "Je suis en route.",
            nameof(MissionConversation),
            conversationId,
            metadata,
            MobileDeviceOwnerType.Customer,
            customerId);

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new HomeServiceDbContext(options);
    }
}
