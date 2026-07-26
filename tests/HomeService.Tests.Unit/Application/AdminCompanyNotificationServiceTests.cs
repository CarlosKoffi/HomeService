using HomeService.Application.Admin;
using HomeService.Application.Auditing;
using HomeService.Domain.Entities;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminCompanyNotificationServiceTests
{
    [Fact]
    public async Task MarkReadAsync_WhenNotificationBelongsToCompany_MarksItRead()
    {
        await using var db = CreateDbContext();
        var notification = SeedPortalNotification(db);
        await db.SaveChangesAsync();

        var result = await new AdminCompanyNotificationService(db).MarkReadAsync(
            notification.CompanyId,
            notification.Id,
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "unit-tests", "notification-read"),
            CancellationToken.None);

        Assert.Equal(AdminCompanyNotificationActionStatus.Ok, result.Status);
        Assert.False(result.PreviousIsRead);
        Assert.True(notification.IsRead);
        var audit = await db.AuditLogEntries.SingleAsync();
        Assert.Equal("AdminCompanyNotificationMarkedRead", audit.Action);
        Assert.Equal(notification.Id, audit.EntityId);
        Assert.Equal("notification-read", audit.CorrelationId);
    }

    [Fact]
    public async Task MarkUnreadAsync_WhenNotificationBelongsToCompany_MarksItUnread()
    {
        await using var db = CreateDbContext();
        var notification = SeedPortalNotification(db);
        notification.MarkRead();
        await db.SaveChangesAsync();

        var result = await new AdminCompanyNotificationService(db).MarkUnreadAsync(
            notification.CompanyId,
            notification.Id,
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "unit-tests", "notification-unread"),
            CancellationToken.None);

        Assert.Equal(AdminCompanyNotificationActionStatus.Ok, result.Status);
        Assert.True(result.PreviousIsRead);
        Assert.False(notification.IsRead);
        var audit = await db.AuditLogEntries.SingleAsync();
        Assert.Equal("AdminCompanyNotificationMarkedUnread", audit.Action);
        Assert.Equal(notification.Id, audit.EntityId);
        Assert.Equal("notification-unread", audit.CorrelationId);
    }

    [Fact]
    public async Task ResendAsync_WhenNotificationBelongsToCompany_CreatesUnreadCopy()
    {
        await using var db = CreateDbContext();
        var notification = SeedPortalNotification(db);
        notification.MarkRead();
        await db.SaveChangesAsync();

        var result = await new AdminCompanyNotificationService(db).ResendAsync(
            notification.CompanyId,
            notification.Id,
            AuditActor.Admin(),
            new AuditRequestContext("127.0.0.1", "unit-tests", "notification-resend"),
            CancellationToken.None);

        Assert.Equal(AdminCompanyNotificationActionStatus.Ok, result.Status);
        var notifications = await db.CompanyPortalNotifications
            .OrderBy(item => item.CreatedAt)
            .ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.Equal(notification.Type, notifications[1].Type);
        Assert.Equal(notification.Title, notifications[1].Title);
        Assert.False(notifications[1].IsRead);
        var audit = await db.AuditLogEntries.SingleAsync();
        Assert.Equal("AdminCompanyNotificationResent", audit.Action);
        Assert.Equal(notifications[1].Id, audit.EntityId);
        Assert.Equal("notification-resend", audit.CorrelationId);
    }

    [Fact]
    public async Task MarkReadAsync_WhenNotificationBelongsToAnotherCompany_ReturnsNotFound()
    {
        await using var db = CreateDbContext();
        var notification = SeedPortalNotification(db);
        await db.SaveChangesAsync();

        var result = await new AdminCompanyNotificationService(db).MarkReadAsync(
            Guid.NewGuid(),
            notification.Id,
            CancellationToken.None);

        Assert.Equal(AdminCompanyNotificationActionStatus.NotFound, result.Status);
        Assert.False(notification.IsRead);
    }

    private static CompanyPortalNotification SeedPortalNotification(HomeServiceDbContext db)
    {
        var notification = new CompanyPortalNotification(
            Guid.NewGuid(),
            null,
            null,
            "MissionDisputeResolved",
            "Litige resolu",
            "Le litige est resolu.",
            "success",
            "missions");
        db.CompanyPortalNotifications.Add(notification);
        return notification;
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomeServiceDbContext(options);
    }
}
