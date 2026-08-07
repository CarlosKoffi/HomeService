using System.Text.Json;
using HomeService.Application.Abstractions;
using HomeService.Contracts.Notifications;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Notifications;

public sealed class MobileNavigationBadgeService(IAppDbContext db)
{
    public async Task<MobileNavigationBadgeResponse> GetForClientAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var paymentActionMissionIds = await db.Missions
            .AsNoTracking()
            .Where(mission => mission.CustomerId == customerId
                && ((mission.Status == MissionStatus.Accepted
                        && mission.QuoteStatus == MissionQuoteStatus.Submitted
                        && mission.PaymentStatus == PaymentStatus.Pending)
                    || (mission.Status == MissionStatus.Completed
                        && mission.CustomerCompletionValidatedAt == null)))
            .Select(mission => mission.Id)
            .ToListAsync(cancellationToken);

        var additionalQuoteMissionIds = await db.MissionAdditionalQuotes
            .AsNoTracking()
            .Where(quote => quote.Mission!.CustomerId == customerId
                && quote.Status == MissionAdditionalQuoteStatus.Submitted)
            .Select(quote => quote.MissionId)
            .ToListAsync(cancellationToken);

        var actionCount = paymentActionMissionIds
            .Concat(additionalQuoteMissionIds)
            .Distinct()
            .Count();
        var messageCount = await CountUnreadMessagesAsync(
            MobileDeviceOwnerType.Customer,
            customerId,
            cancellationToken);

        return new MobileNavigationBadgeResponse(actionCount, messageCount, 0);
    }

    public async Task<MobileNavigationBadgeResponse> GetForProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var actionCount = await db.ProviderMissionAssignments
            .AsNoTracking()
            .CountAsync(assignment => assignment.ProviderId == providerId
                && assignment.Status == ProviderMissionAssignmentStatus.Offered
                && assignment.ExpiresAt > now,
                cancellationToken);
        var messageCount = await CountUnreadMessagesAsync(
            MobileDeviceOwnerType.Provider,
            providerId,
            cancellationToken);

        return new MobileNavigationBadgeResponse(actionCount, messageCount, 0);
    }

    public async Task<MobileNavigationBadgeResponse> GetForCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var alertCount = await db.CompanyPortalNotifications
            .AsNoTracking()
            .CountAsync(notification => notification.CompanyId == companyId && !notification.IsRead, cancellationToken);
        var messageCount = await CountUnreadMessagesAsync(
            MobileDeviceOwnerType.Company,
            companyId,
            cancellationToken);

        return new MobileNavigationBadgeResponse(0, messageCount, alertCount);
    }

    public async Task MarkConversationMessagesReadAsync(
        MobileDeviceOwnerType ownerType,
        Guid ownerId,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var notifications = await db.NotificationOutboxMessages
            .Where(item => item.OwnerType == ownerType
                && item.OwnerId == ownerId
                && item.RelatedEntityType == nameof(MissionConversation)
                && item.RelatedEntityId == conversationId
                && item.ReadAt == null)
            .ToListAsync(cancellationToken);

        if (notifications.Count == 0)
        {
            return;
        }

        var readAt = DateTimeOffset.UtcNow;
        foreach (var notification in notifications)
        {
            notification.MarkRead(readAt);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> CountUnreadMessagesAsync(
        MobileDeviceOwnerType ownerType,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var rows = await db.NotificationOutboxMessages
            .AsNoTracking()
            .Where(item => item.OwnerType == ownerType
                && item.OwnerId == ownerId
                && item.RelatedEntityType == nameof(MissionConversation)
                && item.ReadAt == null
                && (item.Channel == NotificationChannel.MobilePush || item.Channel == NotificationChannel.InApp))
            .OrderByDescending(item => item.CreatedAt)
            .Take(250)
            .Select(item => new
            {
                item.Subject,
                item.Body,
                item.RelatedEntityId,
                item.MetadataJson,
                item.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(item => ExtractMessageKey(
                item.Subject,
                item.Body,
                item.RelatedEntityId,
                item.MetadataJson,
                item.CreatedAt))
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    private static string ExtractMessageKey(
        string subject,
        string body,
        Guid? conversationId,
        string? metadataJson,
        DateTimeOffset createdAt)
    {
        if (!string.IsNullOrWhiteSpace(metadataJson))
        {
            try
            {
                using var document = JsonDocument.Parse(metadataJson);
                if (document.RootElement.TryGetProperty("messageId", out var messageId)
                    && messageId.ValueKind == JsonValueKind.String
                    && Guid.TryParse(messageId.GetString(), out var parsed))
                {
                    return parsed.ToString("D");
                }
            }
            catch (JsonException)
            {
                // Les anciennes notifications restent comptabilisées avec la clé de repli.
            }
        }

        return $"{conversationId:D}|{createdAt:yyyyMMddHHmmss}|{subject}|{body}";
    }
}
