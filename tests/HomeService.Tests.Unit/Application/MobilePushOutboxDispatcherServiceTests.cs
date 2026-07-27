using HomeService.Application.Notifications;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class MobilePushOutboxDispatcherServiceTests
{
    [Fact]
    public async Task DispatchPendingAsync_WhenSenderSucceeds_MarksNotificationSent()
    {
        await using var db = CreateDbContext();
        db.NotificationOutboxMessages.Add(new NotificationOutboxMessage(
            NotificationChannel.MobilePush,
            "token-1",
            "Mission recue",
            "Une mission vous attend.",
            "Mission",
            Guid.NewGuid(),
            """{"missionId":"mission-1"}"""));
        await db.SaveChangesAsync();

        var sut = new MobilePushOutboxDispatcherService(db, new SuccessfulPushSender());

        var result = await sut.DispatchPendingAsync(DateTimeOffset.UtcNow, 10, CancellationToken.None);

        var notification = await db.NotificationOutboxMessages.SingleAsync();
        Assert.Equal(1, result.SentCount);
        Assert.Equal(NotificationStatus.Sent, notification.Status);
        Assert.NotNull(notification.SentAt);
    }

    [Fact]
    public async Task DispatchPendingAsync_WhenSenderFails_MarksNotificationFailedWithoutRealFirebaseCall()
    {
        await using var db = CreateDbContext();
        db.NotificationOutboxMessages.Add(new NotificationOutboxMessage(
            NotificationChannel.MobilePush,
            "token-2",
            "Mission annulee",
            "La mission a ete annulee.",
            "Mission",
            Guid.NewGuid()));
        await db.SaveChangesAsync();

        var sut = new MobilePushOutboxDispatcherService(db, new FailingPushSender());

        var result = await sut.DispatchPendingAsync(DateTimeOffset.UtcNow, 10, CancellationToken.None);

        var notification = await db.NotificationOutboxMessages.SingleAsync();
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(NotificationStatus.Failed, notification.Status);
        Assert.Equal("Firebase mock unavailable", notification.FailureReason);
    }

    [Fact]
    public async Task DispatchPendingAsync_WhenNotificationIsScheduledInFuture_DoesNotSendYet()
    {
        await using var db = CreateDbContext();
        var notification = new NotificationOutboxMessage(
            NotificationChannel.MobilePush,
            "token-3",
            "Rappel mission",
            "Repondez a la mission.",
            "Mission",
            Guid.NewGuid());
        notification.ScheduleAt(DateTimeOffset.UtcNow.AddMinutes(10));
        db.NotificationOutboxMessages.Add(notification);
        await db.SaveChangesAsync();

        var sender = new CountingPushSender();
        var sut = new MobilePushOutboxDispatcherService(db, sender);

        var result = await sut.DispatchPendingAsync(DateTimeOffset.UtcNow, 10, CancellationToken.None);

        var stored = await db.NotificationOutboxMessages.SingleAsync();
        Assert.Equal(0, result.ProcessedCount);
        Assert.Equal(0, sender.SendCount);
        Assert.Equal(NotificationStatus.Pending, stored.Status);
    }

    private sealed class SuccessfulPushSender : IMobilePushSender
    {
        public Task<MobilePushSendResult> SendAsync(
            string deviceToken,
            string title,
            string body,
            IReadOnlyDictionary<string, string>? data,
            CancellationToken cancellationToken)
        {
            Assert.Equal("token-1", deviceToken);
            Assert.Equal("Mission recue", title);
            Assert.NotNull(data);
            return Task.FromResult(MobilePushSendResult.Sent("projects/demo/messages/1"));
        }
    }

    private sealed class FailingPushSender : IMobilePushSender
    {
        public Task<MobilePushSendResult> SendAsync(
            string deviceToken,
            string title,
            string body,
            IReadOnlyDictionary<string, string>? data,
            CancellationToken cancellationToken)
        {
            Assert.Equal("token-2", deviceToken);
            return Task.FromResult(MobilePushSendResult.Failed("Firebase mock unavailable"));
        }
    }

    private sealed class CountingPushSender : IMobilePushSender
    {
        public int SendCount { get; private set; }

        public Task<MobilePushSendResult> SendAsync(
            string deviceToken,
            string title,
            string body,
            IReadOnlyDictionary<string, string>? data,
            CancellationToken cancellationToken)
        {
            SendCount++;
            return Task.FromResult(MobilePushSendResult.Sent("projects/demo/messages/future"));
        }
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
