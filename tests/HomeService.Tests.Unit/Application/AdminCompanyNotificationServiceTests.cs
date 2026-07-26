using HomeService.Application.Admin;
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
            CancellationToken.None);

        Assert.Equal(AdminCompanyNotificationActionStatus.Ok, result.Status);
        Assert.False(result.PreviousIsRead);
        Assert.True(notification.IsRead);
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
            CancellationToken.None);

        Assert.Equal(AdminCompanyNotificationActionStatus.Ok, result.Status);
        Assert.True(result.PreviousIsRead);
        Assert.False(notification.IsRead);
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
            CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.Equal(AdminCompanyNotificationActionStatus.Ok, result.Status);
        var notifications = await db.CompanyPortalNotifications
            .OrderBy(item => item.CreatedAt)
            .ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.Equal(notification.Type, notifications[1].Type);
        Assert.Equal(notification.Title, notifications[1].Title);
        Assert.False(notifications[1].IsRead);
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
