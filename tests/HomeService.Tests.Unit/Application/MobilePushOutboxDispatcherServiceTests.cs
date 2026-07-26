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

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
