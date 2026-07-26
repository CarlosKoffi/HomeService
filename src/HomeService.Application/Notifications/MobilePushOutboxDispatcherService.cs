using System.Text.Json;
using HomeService.Application.Abstractions;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Notifications;

public sealed class MobilePushOutboxDispatcherService(
    IAppDbContext db,
    IMobilePushSender sender)
{
    public async Task<MobilePushOutboxDispatchResult> DispatchPendingAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var notifications = await db.NotificationOutboxMessages
            .Where(notification =>
                notification.Channel == NotificationChannel.MobilePush
                && notification.Status == NotificationStatus.Pending
                && notification.ScheduledAt <= now)
            .OrderBy(notification => notification.ScheduledAt)
            .Take(Math.Clamp(batchSize, 1, 200))
            .ToListAsync(cancellationToken);

        var sentCount = 0;
        var failedCount = 0;
        foreach (var notification in notifications)
        {
            var result = await sender.SendAsync(
                notification.Recipient,
                notification.Subject,
                notification.Body,
                ParseMetadata(notification.MetadataJson),
                cancellationToken);

            if (result.IsSuccess)
            {
                notification.MarkSent();
                sentCount++;
            }
            else
            {
                notification.MarkFailed(result.ErrorMessage ?? "Envoi Firebase impossible.");
                failedCount++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new MobilePushOutboxDispatchResult(notifications.Count, sentCount, failedCount);
    }

    private static IReadOnlyDictionary<string, string>? ParseMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed record MobilePushOutboxDispatchResult(
    int ProcessedCount,
    int SentCount,
    int FailedCount);
