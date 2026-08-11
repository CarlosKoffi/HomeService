using HomeService.Application.Clients;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class ClientNotificationInboxServiceTests
{
    [Fact]
    public async Task ListAndCount_WhenOneEventTargetsSeveralDevices_ReturnOneUnreadNotification()
    {
        await using var db = CreateDbContext();
        var customerId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var metadata = $$"""{"type":"MissionTechnicianProposed","missionId":"{{missionId}}","assignmentId":"assignment-1"}""";
        db.NotificationOutboxMessages.AddRange(
            CreateNotification(customerId, missionId, "token-phone", metadata),
            CreateNotification(customerId, missionId, "token-tablet", metadata));
        await db.SaveChangesAsync();
        var sut = new ClientNotificationInboxService(db);

        var list = await sut.ListAsync(customerId, false, CancellationToken.None);
        var count = await sut.CountUnreadAsync(customerId, CancellationToken.None);

        Assert.Single(list.Notifications);
        Assert.Equal(1, list.UnreadCount);
        Assert.Equal(1, count.UnreadCount);
    }

    [Fact]
    public async Task MarkRead_WhenOneEventTargetsSeveralDevices_MarksWholeEventRead()
    {
        await using var db = CreateDbContext();
        var customerId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var metadata = $$"""{"type":"MissionTechnicianProposed","missionId":"{{missionId}}","assignmentId":"assignment-1"}""";
        var first = CreateNotification(customerId, missionId, "token-phone", metadata);
        db.NotificationOutboxMessages.AddRange(
            first,
            CreateNotification(customerId, missionId, "token-tablet", metadata));
        await db.SaveChangesAsync();
        var sut = new ClientNotificationInboxService(db);

        var result = await sut.MarkReadAsync(customerId, first.Id, CancellationToken.None);
        var count = await sut.CountUnreadAsync(customerId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, count.UnreadCount);
        Assert.All(await db.NotificationOutboxMessages.ToListAsync(), item => Assert.NotNull(item.ReadAt));
    }

    [Fact]
    public async Task MarkAllReadAsync_MarksOnlyCurrentCustomerNotifications()
    {
        await using var db = CreateDbContext();
        var customerId = Guid.NewGuid();
        var otherCustomerId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        db.NotificationOutboxMessages.AddRange(
            CreateNotification(customerId, missionId, "token-phone", "{\"type\":\"MissionStarted\"}"),
            CreateNotification(customerId, missionId, "token-tablet", "{\"type\":\"MissionStarted\"}"),
            CreateNotification(otherCustomerId, missionId, "token-other", "{\"type\":\"MissionStarted\"}"));
        await db.SaveChangesAsync();
        var sut = new ClientNotificationInboxService(db);

        var result = await sut.MarkAllReadAsync(customerId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var notifications = await db.NotificationOutboxMessages.ToListAsync();
        Assert.All(notifications.Where(item => item.OwnerId == customerId), item => Assert.NotNull(item.ReadAt));
        Assert.All(notifications.Where(item => item.OwnerId == otherCustomerId), item => Assert.Null(item.ReadAt));
    }

    private static NotificationOutboxMessage CreateNotification(
        Guid customerId,
        Guid missionId,
        string token,
        string metadata)
        => new(
            NotificationChannel.MobilePush,
            token,
            "Technicien affecte",
            "Un technicien interviendra pour votre mission.",
            "Mission",
            missionId,
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
