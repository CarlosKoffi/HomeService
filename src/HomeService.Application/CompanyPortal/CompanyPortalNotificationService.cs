using HomeService.Application.Abstractions;
using HomeService.Contracts.CompanyPortal;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.CompanyPortal;

public sealed class CompanyPortalNotificationService(IAppDbContext db)
{
    public async Task<CompanyPortalNotificationListResult> ListAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var companyExists = await db.Companies
            .AsNoTracking()
            .AnyAsync(company => company.Id == companyId && company.Status != CompanyStatus.Suspended, cancellationToken);
        if (!companyExists)
        {
            return CompanyPortalNotificationListResult.NotFound();
        }

        var notifications = await db.CompanyPortalNotifications
            .AsNoTracking()
            .Where(notification => notification.CompanyId == companyId)
            .OrderByDescending(notification => notification.OccurredAt)
            .Take(100)
            .Select(notification => new CompanyPortalNotificationResponse(
                notification.Id,
                notification.Type,
                notification.Title,
                notification.Message,
                notification.Tone,
                notification.ActionUrl,
                notification.OccurredAt,
                notification.IsRead,
                notification.CompanyApplicationDocumentId))
            .ToListAsync(cancellationToken);

        var unreadCount = notifications.Count(notification => !notification.IsRead);

        return CompanyPortalNotificationListResult.Ok(new CompanyPortalNotificationListResponse(unreadCount, notifications));
    }

    public async Task<CompanyPortalNotificationActionResult> MarkAllReadAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var companyExists = await db.Companies
            .AsNoTracking()
            .AnyAsync(company => company.Id == companyId && company.Status != CompanyStatus.Suspended, cancellationToken);
        if (!companyExists)
        {
            return CompanyPortalNotificationActionResult.NotFound();
        }

        var unreadNotifications = await db.CompanyPortalNotifications
            .Where(notification => notification.CompanyId == companyId && !notification.IsRead)
            .OrderByDescending(notification => notification.OccurredAt)
            .ToListAsync(cancellationToken);

        foreach (var notification in unreadNotifications)
        {
            notification.MarkRead();
        }

        await db.SaveChangesAsync(cancellationToken);

        return CompanyPortalNotificationActionResult.Ok(unreadNotifications.Count);
    }

    public async Task<CompanyPortalNotificationActionResult> MarkReadAsync(
        Guid companyId,
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        var notification = await db.CompanyPortalNotifications
            .FirstOrDefaultAsync(item => item.Id == notificationId && item.CompanyId == companyId, cancellationToken);
        if (notification is null)
        {
            return CompanyPortalNotificationActionResult.NotFound();
        }

        var wasUnread = !notification.IsRead;
        notification.MarkRead();
        await db.SaveChangesAsync(cancellationToken);

        return CompanyPortalNotificationActionResult.Ok(wasUnread ? 1 : 0);
    }
}

public sealed record CompanyPortalNotificationListResult(
    bool IsSuccess,
    CompanyPortalNotificationListResponse? Response,
    string? Message)
{
    public static CompanyPortalNotificationListResult Ok(CompanyPortalNotificationListResponse response) => new(true, response, null);
    public static CompanyPortalNotificationListResult NotFound() => new(false, null, "Entreprise introuvable ou inactive.");
}

public sealed record CompanyPortalNotificationActionResult(
    bool IsSuccess,
    int UpdatedCount,
    string? Message)
{
    public static CompanyPortalNotificationActionResult Ok(int updatedCount) => new(true, updatedCount, null);
    public static CompanyPortalNotificationActionResult NotFound() => new(false, 0, "Entreprise introuvable ou inactive.");
}
