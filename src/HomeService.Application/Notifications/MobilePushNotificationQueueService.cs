using HomeService.Application.Abstractions;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Notifications;

public sealed class MobilePushNotificationQueueService(IAppDbContext db)
{
    public async Task<int> QueueForOwnerAsync(
        MobileDeviceOwnerType ownerType,
        Guid ownerId,
        string title,
        string body,
        string? relatedEntityType,
        Guid? relatedEntityId,
        string? metadataJson,
        CancellationToken cancellationToken,
        bool saveChanges = true)
    {
        var tokens = await db.MobileDeviceTokens
            .AsNoTracking()
            .Where(token => token.OwnerType == ownerType && token.OwnerId == ownerId && token.IsActive)
            .Select(token => token.Token)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            db.NotificationOutboxMessages.Add(new NotificationOutboxMessage(
                NotificationChannel.MobilePush,
                token,
                title,
                body,
                relatedEntityType,
                relatedEntityId,
                metadataJson));
        }

        if (saveChanges)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return tokens.Count;
    }
}
