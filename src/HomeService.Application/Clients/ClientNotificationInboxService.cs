using HomeService.Application.Abstractions;
using HomeService.Contracts.Clients;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace HomeService.Application.Clients;

public sealed class ClientNotificationInboxService(IAppDbContext db)
{
    public async Task<ClientNotificationListResponse> ListAsync(
        Guid customerId,
        bool unreadOnly,
        CancellationToken cancellationToken)
    {
        var storedNotifications = await BuildCustomerNotificationQuery(customerId)
            .OrderByDescending(notification => notification.ScheduledAt)
            .Take(400)
            .ToListAsync(cancellationToken);

        var groups = storedNotifications
            .GroupBy(BuildLogicalKey)
            .Select(group => new
            {
                Latest = group.OrderByDescending(item => item.ScheduledAt).First(),
                IsRead = group.All(item => item.ReadAt != null)
            })
            .OrderByDescending(group => group.Latest.ScheduledAt)
            .ToList();

        var unreadCount = groups.Count(group => !group.IsRead);
        var notifications = groups
            .Where(group => !unreadOnly || !group.IsRead)
            .Take(80)
            .Select(group => new ClientNotificationResponse(
                group.Latest.Id,
                group.Latest.Subject,
                group.Latest.Body,
                group.Latest.Status.ToString(),
                group.IsRead,
                group.Latest.ScheduledAt,
                group.Latest.SentAt,
                group.IsRead ? group.Latest.ReadAt : null,
                group.Latest.RelatedEntityType,
                group.Latest.RelatedEntityId,
                group.Latest.MetadataJson))
            .ToList();

        return new ClientNotificationListResponse(unreadCount, notifications);
    }

    public async Task<ClientNotificationUnreadCountResponse> CountUnreadAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var notifications = await BuildCustomerNotificationQuery(customerId)
            .OrderByDescending(notification => notification.ScheduledAt)
            .Take(400)
            .ToListAsync(cancellationToken);
        var unreadCount = notifications
            .GroupBy(BuildLogicalKey)
            .Count(group => group.Any(notification => notification.ReadAt == null));

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

        var logicalKey = BuildLogicalKey(notification);
        var relatedNotifications = await BuildCustomerNotificationQuery(customerId).ToListAsync(cancellationToken);
        var readAt = DateTimeOffset.UtcNow;
        foreach (var relatedNotification in relatedNotifications.Where(item => BuildLogicalKey(item) == logicalKey))
        {
            relatedNotification.MarkRead(readAt);
        }
        await db.SaveChangesAsync(cancellationToken);
        return ClientNotificationActionResult.Ok("Notification marquee comme lue.");
    }

    private IQueryable<Domain.Entities.NotificationOutboxMessage> BuildCustomerNotificationQuery(Guid customerId)
    {
        return db.NotificationOutboxMessages
            .Where(notification =>
                (notification.Channel == NotificationChannel.MobilePush
                    || notification.Channel == NotificationChannel.InApp)
                && notification.OwnerType == MobileDeviceOwnerType.Customer
                && notification.OwnerId == customerId);
    }

    private static string BuildLogicalKey(NotificationOutboxMessage notification)
    {
        var metadataKey = TryBuildMetadataKey(notification.MetadataJson);
        return metadataKey is not null
            ? $"metadata:{metadataKey}"
            : $"fallback:{notification.RelatedEntityType}:{notification.RelatedEntityId}:{notification.Subject}:{notification.Body}";
    }

    private static string? TryBuildMetadataKey(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)) return null;

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            var root = document.RootElement;
            var type = ReadProperty(root, "type");
            var missionId = ReadProperty(root, "missionId");
            var assignmentId = ReadProperty(root, "assignmentId");
            if (string.IsNullOrWhiteSpace(type) && string.IsNullOrWhiteSpace(missionId)) return null;
            return $"{type}:{missionId}:{assignmentId}";
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ReadProperty(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) ? value.ToString() : string.Empty;
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
