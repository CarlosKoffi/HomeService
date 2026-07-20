using HomeService.Application.Abstractions;
using HomeService.Contracts.Notifications;
using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminNotificationDeliveryRuleService(IAppDbContext db)
{
    private static readonly IReadOnlyList<NotificationDeliveryRuleSeed> DefaultRules =
    [
        new("CompanyDocumentRejected", "Piece entreprise refusee", "Company", true, false, true, true),
        new("CompanyDocumentNeedsReplacement", "Complement requis sur dossier entreprise", "Company", true, false, true, true),
        new("CompanyApplicationApproved", "Dossier entreprise valide", "Company", true, false, true, true),
        new("CompanyActivationLinkCreated", "Lien d'activation entreprise", "Company", true, false, true, true),
        new("InterimCandidateReceived", "Nouvelle demande interimaire", "Company", true, false, false, false),
        new("InterimCandidateApproved", "Candidature interimaire acceptee", "Provider", false, true, false, true),
        new("MissionAssignedToProvider", "Mission affectee au prestataire", "Provider", false, true, false, true),
        new("MissionQuoteSentToCustomer", "Devis mission envoye au client", "Customer", false, true, true, true),
        new("MissionStatusChanged", "Suivi de mission", "Mixed", true, true, false, false)
    ];

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

        rule.Update(
            request.Label,
            request.Audience,
            request.PortalEnabled,
            request.MobileAppEnabled,
            request.EmailEnabled,
            request.WhatsAppEnabled);

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
            db.NotificationDeliveryRules.Add(new NotificationDeliveryRule(
                seed.EventKey,
                seed.Label,
                seed.Audience,
                seed.PortalEnabled,
                seed.MobileAppEnabled,
                seed.EmailEnabled,
                seed.WhatsAppEnabled));
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

        if (!request.PortalEnabled
            && !request.MobileAppEnabled
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
            rule.CreatedAt,
            rule.UpdatedAt);
    }

    private sealed record NotificationDeliveryRuleSeed(
        string EventKey,
        string Label,
        string Audience,
        bool PortalEnabled,
        bool MobileAppEnabled,
        bool EmailEnabled,
        bool WhatsAppEnabled);
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
