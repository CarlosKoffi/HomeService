using HomeService.Application.Abstractions;
using HomeService.Application.Auditing;
using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminCompanyNotificationService(IAppDbContext db)
{
    public async Task<AdminCompanyNotificationActionResult> MarkReadAsync(
        Guid companyId,
        Guid notificationId,
        CancellationToken cancellationToken)
        => await MarkReadAsync(companyId, notificationId, null, null, cancellationToken);

    public async Task<AdminCompanyNotificationActionResult> MarkReadAsync(
        Guid companyId,
        Guid notificationId,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var notification = await FindAsync(companyId, notificationId, cancellationToken);
        if (notification is null)
        {
            return AdminCompanyNotificationActionResult.NotFound();
        }

        var previousIsRead = notification.IsRead;
        notification.MarkRead();
        AddAuditLog(
            actor,
            auditContext,
            "AdminCompanyNotificationMarkedRead",
            "Notification entreprise marquee comme lue par l'administration.",
            notification,
            previousIsRead);
        await db.SaveChangesAsync(cancellationToken);

        return AdminCompanyNotificationActionResult.Ok(notification, previousIsRead);
    }

    public async Task<AdminCompanyNotificationActionResult> MarkUnreadAsync(
        Guid companyId,
        Guid notificationId,
        CancellationToken cancellationToken)
        => await MarkUnreadAsync(companyId, notificationId, null, null, cancellationToken);

    public async Task<AdminCompanyNotificationActionResult> MarkUnreadAsync(
        Guid companyId,
        Guid notificationId,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var notification = await FindAsync(companyId, notificationId, cancellationToken);
        if (notification is null)
        {
            return AdminCompanyNotificationActionResult.NotFound();
        }

        var previousIsRead = notification.IsRead;
        notification.MarkUnread();
        AddAuditLog(
            actor,
            auditContext,
            "AdminCompanyNotificationMarkedUnread",
            "Notification entreprise remise en non lue par l'administration.",
            notification,
            previousIsRead);
        await db.SaveChangesAsync(cancellationToken);

        return AdminCompanyNotificationActionResult.Ok(notification, previousIsRead);
    }

    public async Task<AdminCompanyNotificationActionResult> ResendAsync(
        Guid companyId,
        Guid notificationId,
        CancellationToken cancellationToken)
        => await ResendAsync(companyId, notificationId, null, null, cancellationToken);

    public async Task<AdminCompanyNotificationActionResult> ResendAsync(
        Guid companyId,
        Guid notificationId,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var notification = await FindAsync(companyId, notificationId, cancellationToken);
        if (notification is null)
        {
            return AdminCompanyNotificationActionResult.NotFound();
        }

        var copy = new CompanyPortalNotification(
            notification.CompanyId,
            notification.CompanyApplicationId,
            notification.CompanyApplicationDocumentId,
            notification.Type,
            notification.Title,
            notification.Message,
            notification.Tone,
            notification.ActionUrl);

        db.CompanyPortalNotifications.Add(copy);
        AddAuditLog(
            actor,
            auditContext,
            "AdminCompanyNotificationResent",
            "Notification entreprise renvoyee sur le portail par l'administration.",
            copy,
            previousIsRead: null);
        await db.SaveChangesAsync(cancellationToken);

        return AdminCompanyNotificationActionResult.Ok(copy, previousIsRead: false);
    }

    private async Task<CompanyPortalNotification?> FindAsync(
        Guid companyId,
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        return await db.CompanyPortalNotifications
            .FirstOrDefaultAsync(notification => notification.Id == notificationId && notification.CompanyId == companyId, cancellationToken);
    }

    private void AddAuditLog(
        AuditActor? actor,
        AuditRequestContext? auditContext,
        string action,
        string summary,
        CompanyPortalNotification notification,
        bool? previousIsRead)
    {
        if (actor is null)
        {
            return;
        }

        db.AuditLogEntries.Add(AuditLogFactory.Create(
            actor,
            action,
            nameof(CompanyPortalNotification),
            notification.Id,
            summary,
            auditContext,
            previousIsRead is null ? null : new { IsRead = previousIsRead },
            new
            {
                notification.CompanyId,
                notification.Type,
                notification.Title,
                notification.IsRead
            }));
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
