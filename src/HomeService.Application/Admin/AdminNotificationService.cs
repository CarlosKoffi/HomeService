using HomeService.Application.Abstractions;
using HomeService.Application.Auditing;
using HomeService.Contracts.Notifications;
using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminNotificationService(IAppDbContext db)
{
    public async Task<AdminNotificationActionResult> RetryAsync(
        Guid notificationId,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var notification = await FindAsync(notificationId, cancellationToken);
        if (notification is null)
        {
            return AdminNotificationActionResult.NotFound();
        }

        try
        {
            notification.Retry();
        }
        catch (InvalidOperationException exception)
        {
            return AdminNotificationActionResult.InvalidTransition(exception.Message);
        }

        var response = ToResponse(notification);
        AddAuditLog(actor, auditContext, "AdminNotificationRetried", notification.Id, "Notification relancee.", response);
        await db.SaveChangesAsync(cancellationToken);

        return AdminNotificationActionResult.Ok(response);
    }

    public Task<AdminNotificationActionResult> RetryAsync(Guid notificationId, CancellationToken cancellationToken)
        => RetryAsync(notificationId, null, null, cancellationToken);

    public async Task<AdminNotificationActionResult> CancelAsync(
        Guid notificationId,
        string? reason,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var notification = await FindAsync(notificationId, cancellationToken);
        if (notification is null)
        {
            return AdminNotificationActionResult.NotFound();
        }

        try
        {
            notification.Cancel(reason);
        }
        catch (InvalidOperationException exception)
        {
            return AdminNotificationActionResult.InvalidTransition(exception.Message);
        }

        var response = ToResponse(notification);
        AddAuditLog(actor, auditContext, "AdminNotificationCancelled", notification.Id, "Notification annulee.", response);
        await db.SaveChangesAsync(cancellationToken);

        return AdminNotificationActionResult.Ok(response);
    }

    public Task<AdminNotificationActionResult> CancelAsync(Guid notificationId, string? reason, CancellationToken cancellationToken)
        => CancelAsync(notificationId, reason, null, null, cancellationToken);

    public async Task<AdminNotificationActionResult> MarkSentAsync(
        Guid notificationId,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var notification = await FindAsync(notificationId, cancellationToken);
        if (notification is null)
        {
            return AdminNotificationActionResult.NotFound();
        }

        notification.MarkSent();
        var response = ToResponse(notification);
        AddAuditLog(actor, auditContext, "AdminNotificationMarkedSent", notification.Id, "Notification marquee envoyee.", response);
        await db.SaveChangesAsync(cancellationToken);

        return AdminNotificationActionResult.Ok(response);
    }

    public Task<AdminNotificationActionResult> MarkSentAsync(Guid notificationId, CancellationToken cancellationToken)
        => MarkSentAsync(notificationId, null, null, cancellationToken);

    private async Task<NotificationOutboxMessage?> FindAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        return await db.NotificationOutboxMessages
            .FirstOrDefaultAsync(notification => notification.Id == notificationId, cancellationToken);
    }

    private static NotificationOutboxMessageResponse ToResponse(NotificationOutboxMessage notification)
    {
        return new NotificationOutboxMessageResponse(
            notification.Id,
            notification.Channel.ToString(),
            notification.Status.ToString(),
            notification.Recipient,
            notification.Subject,
            notification.Body,
            notification.RelatedEntityType,
            notification.RelatedEntityId,
            notification.ScheduledAt,
            notification.SentAt,
            notification.FailureReason);
    }

    private void AddAuditLog(
        AuditActor? actor,
        AuditRequestContext? auditContext,
        string action,
        Guid notificationId,
        string summary,
        NotificationOutboxMessageResponse response)
    {
        if (actor is null)
        {
            return;
        }

        db.AuditLogEntries.Add(AuditLogFactory.Create(
            actor,
            action,
            nameof(NotificationOutboxMessage),
            notificationId,
            summary,
            auditContext,
            after: response));
    }
}

public sealed record AdminNotificationActionResult(
    AdminNotificationActionStatus Status,
    NotificationOutboxMessageResponse? Response,
    string? Message)
{
    public static AdminNotificationActionResult Ok(NotificationOutboxMessageResponse response)
        => new(AdminNotificationActionStatus.Ok, response, null);

    public static AdminNotificationActionResult NotFound()
        => new(AdminNotificationActionStatus.NotFound, null, "La notification n'existe plus.");

    public static AdminNotificationActionResult InvalidTransition(string message)
        => new(AdminNotificationActionStatus.InvalidTransition, null, message);
}

public enum AdminNotificationActionStatus
{
    Ok,
    NotFound,
    InvalidTransition
}
