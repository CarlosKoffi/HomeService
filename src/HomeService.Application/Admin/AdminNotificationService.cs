using HomeService.Application.Abstractions;
using HomeService.Contracts.Notifications;
using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminNotificationService(IAppDbContext db)
{
    public async Task<AdminNotificationActionResult> RetryAsync(Guid notificationId, CancellationToken cancellationToken)
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

        return AdminNotificationActionResult.Ok(ToResponse(notification));
    }

    public async Task<AdminNotificationActionResult> CancelAsync(Guid notificationId, string? reason, CancellationToken cancellationToken)
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

        return AdminNotificationActionResult.Ok(ToResponse(notification));
    }

    public async Task<AdminNotificationActionResult> MarkSentAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        var notification = await FindAsync(notificationId, cancellationToken);
        if (notification is null)
        {
            return AdminNotificationActionResult.NotFound();
        }

        notification.MarkSent();
        return AdminNotificationActionResult.Ok(ToResponse(notification));
    }

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
