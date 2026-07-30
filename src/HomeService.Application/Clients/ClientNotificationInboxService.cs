using HomeService.Application.Abstractions;
using HomeService.Contracts.Clients;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Clients;

public sealed class ClientNotificationInboxService(IAppDbContext db)
{
    public async Task<ClientNotificationListResponse> ListAsync(
        Guid customerId,
        bool unreadOnly,
        CancellationToken cancellationToken)
    {
        var query = BuildCustomerNotificationQuery(customerId);
        var unreadCount = await query.CountAsync(notification => notification.ReadAt == null, cancellationToken);

        if (unreadOnly)
        {
            query = query.Where(notification => notification.ReadAt == null);
        }

        var notifications = await query
            .OrderByDescending(notification => notification.ScheduledAt)
            .Take(80)
            .Select(notification => new ClientNotificationResponse(
                notification.Id,
                notification.Subject,
                notification.Body,
                notification.Status.ToString(),
                notification.ReadAt != null,
                notification.ScheduledAt,
                notification.SentAt,
                notification.ReadAt,
                notification.RelatedEntityType,
                notification.RelatedEntityId,
                notification.MetadataJson))
            .ToListAsync(cancellationToken);

        return new ClientNotificationListResponse(unreadCount, notifications);
    }

    public async Task<ClientNotificationUnreadCountResponse> CountUnreadAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var unreadCount = await BuildCustomerNotificationQuery(customerId)
            .CountAsync(notification => notification.ReadAt == null, cancellationToken);

        return new ClientNotificationUnreadCountResponse(unreadCount);
    }

    public async Task<ClientNotificationActionResult> MarkReadAsync(
        Guid customerId,
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        var notification = await BuildCustomerNotificationQuery(customerId)
            .FirstOrDefaultAsync(item => item.Id == notificationId, cancellationToken);

        if (notification is null)
        {
            return ClientNotificationActionResult.NotFound("Notification introuvable.");
        }

        notification.MarkRead(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return ClientNotificationActionResult.Ok("Notification marquee comme lue.");
    }

    private IQueryable<Domain.Entities.NotificationOutboxMessage> BuildCustomerNotificationQuery(Guid customerId)
    {
        return db.NotificationOutboxMessages
            .Where(notification =>
                notification.Channel == NotificationChannel.MobilePush
                && notification.OwnerType == MobileDeviceOwnerType.Customer
                && notification.OwnerId == customerId);
    }
}

public sealed record ClientNotificationActionResult(
    bool IsSuccess,
    string Message)
{
    public static ClientNotificationActionResult Ok(string message)
        => new(true, message);

    public static ClientNotificationActionResult NotFound(string message)
        => new(false, message);
}
