using HomeService.Application.Abstractions;
using HomeService.Application.Notifications;
using HomeService.Contracts.Notifications;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminNotificationDeliveryRuleService(IAppDbContext db)
{
    private static readonly IReadOnlyList<NotificationDeliveryRuleSeed> DefaultRules =
        NotificationTemplateCatalog.Defaults
            .Select(seed => new NotificationDeliveryRuleSeed(
                seed.EventKey,
                seed.Label,
                seed.Audience,
                seed.Channels.Contains(NotificationTemplateChannel.Email),
                seed.Channels.Contains(NotificationTemplateChannel.WhatsApp),
                seed.SubjectTemplate,
                seed.BodyTemplate))
            .ToList();

    public async Task<IReadOnlyList<NotificationDeliveryRuleResponse>> ListAsync(CancellationToken cancellationToken)
    {
        await EnsureDefaultsAsync(cancellationToken);

        return await db.NotificationDeliveryRules
            .AsNoTracking()
            .OrderBy(rule => rule.Audience)
            .ThenBy(rule => rule.Label)
            .Select(rule => ToResponse(rule))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminNotificationDeliveryRuleResult> UpdateAsync(
        Guid ruleId,
        UpdateNotificationDeliveryRuleRequest request,
        CancellationToken cancellationToken)
    {
        var rule = await db.NotificationDeliveryRules
            .FirstOrDefaultAsync(item => item.Id == ruleId, cancellationToken);

        if (rule is null)
        {
            return AdminNotificationDeliveryRuleResult.NotFound();
        }

        var validation = Validate(request);
        if (validation is not null)
        {
            return AdminNotificationDeliveryRuleResult.ValidationFailed(validation);
        }

        var normalized = NormalizeChannels(request.Audience, request.EmailEnabled, request.WhatsAppEnabled);

        rule.Update(
            request.Label,
            request.Audience,
            normalized.PortalEnabled,
            normalized.MobileAppEnabled,
            normalized.EmailEnabled,
            normalized.WhatsAppEnabled,
            request.SubjectTemplate,
            request.BodyTemplate);

        await db.SaveChangesAsync(cancellationToken);

        return AdminNotificationDeliveryRuleResult.Ok(ToResponse(rule));
    }

    private async Task EnsureDefaultsAsync(CancellationToken cancellationToken)
    {
        var existingKeys = await db.NotificationDeliveryRules
            .Select(rule => rule.EventKey)
            .ToListAsync(cancellationToken);
        var existing = existingKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var hasAddedRule = false;
        foreach (var seed in DefaultRules.Where(seed => !existing.Contains(seed.EventKey)))
        {
            var normalized = NormalizeChannels(seed.Audience, seed.EmailEnabled, seed.WhatsAppEnabled);
            db.NotificationDeliveryRules.Add(new NotificationDeliveryRule(
                seed.EventKey,
                seed.Label,
                seed.Audience,
                normalized.PortalEnabled,
                normalized.MobileAppEnabled,
                normalized.EmailEnabled,
                normalized.WhatsAppEnabled,
                seed.SubjectTemplate,
                seed.BodyTemplate));
            hasAddedRule = true;
        }

        if (hasAddedRule)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static string? Validate(UpdateNotificationDeliveryRuleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Label))
        {
            return "Le libelle est obligatoire.";
        }

        if (string.IsNullOrWhiteSpace(request.Audience))
        {
            return "L'audience est obligatoire.";
        }

        if (!IsKnownAudience(request.Audience))
        {
            return "Audience invalide. Utilisez Company, Provider, Customer ou Mixed.";
        }

        var hasAutomaticChannel = NotificationDeliveryPreferenceService.IsPortalAutomatic(request.Audience)
            || NotificationDeliveryPreferenceService.IsMobileAppAutomatic(request.Audience);

        if (!hasAutomaticChannel
            && !request.EmailEnabled
            && !request.WhatsAppEnabled)
        {
            return "Activez au moins un canal.";
        }

        return null;
    }

    private static bool IsKnownAudience(string audience)
    {
        return audience.Trim() is "Company" or "Provider" or "Customer" or "Mixed";
    }

    private static NotificationDeliveryPreference NormalizeChannels(string audience, bool emailEnabled, bool whatsAppEnabled)
    {
        return new NotificationDeliveryPreference(
            NotificationDeliveryPreferenceService.IsPortalAutomatic(audience),
            NotificationDeliveryPreferenceService.IsMobileAppAutomatic(audience),
            emailEnabled,
            whatsAppEnabled,
            null,
            null);
    }

    private static NotificationDeliveryRuleResponse ToResponse(NotificationDeliveryRule rule)
    {
        return new NotificationDeliveryRuleResponse(
            rule.Id,
            rule.EventKey,
            rule.Label,
            rule.Audience,
            rule.PortalEnabled,
            rule.MobileAppEnabled,
            rule.EmailEnabled,
            rule.WhatsAppEnabled,
            rule.SubjectTemplate,
            rule.BodyTemplate,
            rule.CreatedAt,
            rule.UpdatedAt);
    }

    private sealed record NotificationDeliveryRuleSeed(
        string EventKey,
        string Label,
        string Audience,
        bool EmailEnabled,
        bool WhatsAppEnabled,
        string SubjectTemplate,
        string BodyTemplate);
}

public sealed record AdminNotificationDeliveryRuleResult(
    AdminNotificationDeliveryRuleStatus Status,
    NotificationDeliveryRuleResponse? Response,
    string? Message)
{
    public static AdminNotificationDeliveryRuleResult Ok(NotificationDeliveryRuleResponse response)
        => new(AdminNotificationDeliveryRuleStatus.Ok, response, null);

    public static AdminNotificationDeliveryRuleResult NotFound()
        => new(AdminNotificationDeliveryRuleStatus.NotFound, null, "La regle de notification n'existe plus.");

    public static AdminNotificationDeliveryRuleResult ValidationFailed(string message)
        => new(AdminNotificationDeliveryRuleStatus.ValidationFailed, null, message);
}

public enum AdminNotificationDeliveryRuleStatus
{
    Ok,
    NotFound,
    ValidationFailed
}
