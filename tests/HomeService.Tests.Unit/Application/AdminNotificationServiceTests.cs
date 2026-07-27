using HomeService.Application.Admin;
using HomeService.Application.Auditing;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminNotificationServiceTests
{
    [Fact]
    public async Task RetryAsync_WhenNotificationFailed_RequeuesIt()
    {
        await using var db = CreateDbContext();
        var notification = CreateOutboxNotification();
        notification.MarkFailed("Erreur fournisseur");
        db.NotificationOutboxMessages.Add(notification);
        await db.SaveChangesAsync();

        var result = await new AdminNotificationService(db).RetryAsync(notification.Id, CancellationToken.None);

        Assert.Equal(AdminNotificationActionStatus.Ok, result.Status);
        Assert.Equal(NotificationStatus.Pending.ToString(), result.Response!.Status);
        Assert.Null(notification.FailureReason);
    }

    [Fact]
    public async Task CancelAsync_WhenNotificationAlreadySent_IsRejected()
    {
        await using var db = CreateDbContext();
        var notification = CreateOutboxNotification();
        notification.MarkSent();
        db.NotificationOutboxMessages.Add(notification);
        await db.SaveChangesAsync();

        var result = await new AdminNotificationService(db).CancelAsync(
            notification.Id,
            "Annulation admin",
            CancellationToken.None);

        Assert.Equal(AdminNotificationActionStatus.InvalidTransition, result.Status);
        Assert.Equal(NotificationStatus.Sent, notification.Status);
    }

    [Fact]
    public async Task MarkSentAsync_WhenNotificationPending_MarksItSent()
    {
        await using var db = CreateDbContext();
        var notification = CreateOutboxNotification();
        db.NotificationOutboxMessages.Add(notification);
        await db.SaveChangesAsync();

        var result = await new AdminNotificationService(db).MarkSentAsync(notification.Id, CancellationToken.None);

        Assert.Equal(AdminNotificationActionStatus.Ok, result.Status);
        Assert.Equal(NotificationStatus.Sent.ToString(), result.Response!.Status);
        Assert.NotNull(notification.SentAt);
    }

    [Fact]
    public async Task RetryAsync_WhenAuditActorIsProvided_CreatesAuditLog()
    {
        await using var db = CreateDbContext();
        var notification = CreateOutboxNotification();
        notification.MarkFailed("Erreur fournisseur");
        db.NotificationOutboxMessages.Add(notification);
        await db.SaveChangesAsync();

        var result = await new AdminNotificationService(db).RetryAsync(
            notification.Id,
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "tests", "notification-audit"),
            CancellationToken.None);

        Assert.Equal(AdminNotificationActionStatus.Ok, result.Status);
        var log = Assert.Single(db.AuditLogEntries);
        Assert.Equal("AdminNotificationRetried", log.Action);
        Assert.Equal(notification.Id, log.EntityId);
        Assert.Equal("notification-audit", log.CorrelationId);
    }

    [Fact]
    public async Task CancelAsync_WhenAuditActorIsProvided_CancelsAndCreatesAuditLog()
    {
        await using var db = CreateDbContext();
        var notification = CreateOutboxNotification();
        db.NotificationOutboxMessages.Add(notification);
        await db.SaveChangesAsync();

        var result = await new AdminNotificationService(db).CancelAsync(
            notification.Id,
            "Doublon traite par telephone",
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "tests", "notification-cancel"),
            CancellationToken.None);

        Assert.Equal(AdminNotificationActionStatus.Ok, result.Status);
        Assert.Equal(NotificationStatus.Cancelled.ToString(), result.Response!.Status);
        Assert.Equal("Doublon traite par telephone", notification.FailureReason);

        var log = Assert.Single(db.AuditLogEntries);
        Assert.Equal("AdminNotificationCancelled", log.Action);
        Assert.Equal(notification.Id, log.EntityId);
        Assert.Equal("notification-cancel", log.CorrelationId);
    }

    [Fact]
    public async Task MarkSentAsync_WhenAuditActorIsProvided_MarksSentAndCreatesAuditLog()
    {
        await using var db = CreateDbContext();
        var notification = CreateOutboxNotification();
        db.NotificationOutboxMessages.Add(notification);
        await db.SaveChangesAsync();

        var result = await new AdminNotificationService(db).MarkSentAsync(
            notification.Id,
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "tests", "notification-sent"),
            CancellationToken.None);

        Assert.Equal(AdminNotificationActionStatus.Ok, result.Status);
        Assert.Equal(NotificationStatus.Sent.ToString(), result.Response!.Status);
        Assert.NotNull(notification.SentAt);

        var log = Assert.Single(db.AuditLogEntries);
        Assert.Equal("AdminNotificationMarkedSent", log.Action);
        Assert.Equal(notification.Id, log.EntityId);
        Assert.Equal("notification-sent", log.CorrelationId);
    }

    private static NotificationOutboxMessage CreateOutboxNotification()
        => new(
            NotificationChannel.Email,
            "contact@wele.ci",
            "Sujet",
            "Message",
            "Mission",
            Guid.NewGuid());

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
