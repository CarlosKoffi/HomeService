using HomeService.Application.Abstractions;
using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminCompanyNotificationService(IAppDbContext db)
{
    public async Task<AdminCompanyNotificationActionResult> MarkReadAsync(
        Guid companyId,
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        var notification = await FindAsync(companyId, notificationId, cancellationToken);
        if (notification is null)
        {
            return AdminCompanyNotificationActionResult.NotFound();
        }

        var previousIsRead = notification.IsRead;
        notification.MarkRead();
        return AdminCompanyNotificationActionResult.Ok(notification, previousIsRead);
    }

    public async Task<AdminCompanyNotificationActionResult> MarkUnreadAsync(
        Guid companyId,
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        var notification = await FindAsync(companyId, notificationId, cancellationToken);
        if (notification is null)
        {
            return AdminCompanyNotificationActionResult.NotFound();
        }

        var previousIsRead = notification.IsRead;
        notification.MarkUnread();
        return AdminCompanyNotificationActionResult.Ok(notification, previousIsRead);
    }

    private async Task<CompanyPortalNotification?> FindAsync(
        Guid companyId,
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        return await db.CompanyPortalNotifications
            .FirstOrDefaultAsync(notification => notification.Id == notificationId && notification.CompanyId == companyId, cancellationToken);
    }
}

public sealed record AdminCompanyNotificationActionResult(
    AdminCompanyNotificationActionStatus Status,
    CompanyPortalNotification? Notification,
    bool? PreviousIsRead,
    string? Message)
{
    public static AdminCompanyNotificationActionResult Ok(CompanyPortalNotification notification, bool previousIsRead)
        => new(AdminCompanyNotificationActionStatus.Ok, notification, previousIsRead, null);

    public static AdminCompanyNotificationActionResult NotFound()
        => new(AdminCompanyNotificationActionStatus.NotFound, null, null, "Notification entreprise introuvable.");
}

public enum AdminCompanyNotificationActionStatus
{
    Ok = 0,
    NotFound = 1
}
