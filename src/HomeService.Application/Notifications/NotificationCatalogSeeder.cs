using HomeService.Application.Abstractions;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Notifications;

public sealed class NotificationCatalogSeeder(IAppDbContext db)
{
    public async Task EnsureDefaultsAsync(CancellationToken cancellationToken)
    {
        await EnsureDeliveryRulesAsync(cancellationToken);
        await EnsureTemplatesAsync(cancellationToken);
    }

    private async Task EnsureDeliveryRulesAsync(CancellationToken cancellationToken)
    {
        var existingKeys = await db.NotificationDeliveryRules
            .Select(rule => rule.EventKey)
            .ToListAsync(cancellationToken);
        var existing = existingKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var hasAddedRule = false;
        foreach (var seed in NotificationTemplateCatalog.Defaults.Where(seed => !existing.Contains(seed.EventKey)))
        {
            var channels = NormalizeChannels(seed.Audience);
            db.NotificationDeliveryRules.Add(new NotificationDeliveryRule(
                seed.EventKey,
                seed.Label,
                seed.Audience,
                channels.PortalEnabled,
                channels.MobileAppEnabled,
                seed.Channels.Contains(NotificationTemplateChannel.Email),
                seed.Channels.Contains(NotificationTemplateChannel.WhatsApp),
                seed.SubjectTemplate,
                seed.BodyTemplate));
            hasAddedRule = true;
        }

        if (hasAddedRule)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task EnsureTemplatesAsync(CancellationToken cancellationToken)
    {
        var existing = await db.NotificationTemplates
            .Select(template => new { template.EventKey, template.Channel })
            .ToListAsync(cancellationToken);
        var existingKeys = existing
            .Select(template => BuildTemplateKey(template.EventKey, template.Channel))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var hasAddedTemplate = false;
        foreach (var seed in NotificationTemplateCatalog.Defaults)
        {
            foreach (var channel in seed.Channels)
            {
                var key = BuildTemplateKey(seed.EventKey, channel);
                if (existingKeys.Contains(key))
                {
                    continue;
                }

                db.NotificationTemplates.Add(new NotificationTemplate(
                    seed.EventKey,
                    channel,
                    seed.Label,
                    seed.Audience,
                    seed.SubjectTemplate,
                    seed.BodyTemplate,
                    seed.AvailableVariables));
                hasAddedTemplate = true;
            }
        }

        if (hasAddedTemplate)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static string BuildTemplateKey(string eventKey, NotificationTemplateChannel channel)
        => $"{eventKey}|{channel}";

    private static NotificationCatalogRuleChannels NormalizeChannels(string audience)
    {
        var normalized = string.IsNullOrWhiteSpace(audience) ? "Mixed" : audience.Trim();
        return new NotificationCatalogRuleChannels(
            normalized is "Company" or "Mixed",
            normalized is "Provider" or "Customer" or "Mixed");
    }

    private sealed record NotificationCatalogRuleChannels(bool PortalEnabled, bool MobileAppEnabled);
}
