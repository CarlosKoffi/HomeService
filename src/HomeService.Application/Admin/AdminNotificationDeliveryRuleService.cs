using HomeService.Application.Abstractions;
using HomeService.Application.Notifications;
using HomeService.Contracts.Notifications;
using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminNotificationDeliveryRuleService(IAppDbContext db)
{
    private static readonly IReadOnlyList<NotificationDeliveryRuleSeed> DefaultRules =
    [
        new("CompanyDocumentRejected", "Piece entreprise refusee", "Company", true, false, true, true, "Piece a reprendre", "{NomEntreprise}, une piece de votre dossier demande une correction."),
        new("CompanyDocumentNeedsReplacement", "Complement requis sur dossier entreprise", "Company", true, false, true, true, "Complement requis", "{NomEntreprise}, notre equipe demande un complement sur votre dossier."),
        new("CompanyDocumentReopened", "Piece entreprise reouverte", "Company", true, false, true, true, "Piece reouverte", "{NomEntreprise}, une piece de votre dossier a ete remise en verification."),
        new("CompanyApplicationRejected", "Dossier entreprise refuse", "Company", true, false, true, true, "Dossier refuse", "{NomEntreprise}, votre demande partenaire n'a pas pu etre validee pour le moment."),
        new("CompanyApplicationReopened", "Dossier entreprise reouvert", "Company", true, false, true, true, "Dossier reouvert", "{NomEntreprise}, votre dossier partenaire est de nouveau en analyse."),
        new("CompanyApplicationMoreInformationRequested", "Complement requis sur dossier entreprise", "Company", true, false, true, true, "Complement requis", "{NomEntreprise}, un complement est necessaire pour terminer l'analyse."),
        new("CompanyApplicationApproved", "Dossier entreprise valide", "Company", true, false, true, true, "Dossier valide", "{NomEntreprise}, votre entreprise est validee sur Wele."),
        new("CompanyActivationLinkCreated", "Lien d'activation entreprise", "Company", true, false, true, true, "Activation de votre portail", "{NomEntreprise}, votre lien d'activation est pret."),
        new("InterimCandidateReceived", "Nouvelle demande interimaire", "Company", true, false, false, false, "Nouvelle candidature", "{NomEntreprise}, {NomPrestataire} souhaite collaborer avec vous."),
        new("InterimCandidateApproved", "Candidature interimaire acceptee", "Provider", false, true, false, true, "Candidature acceptee", "{NomPrestataire}, {NomEntreprise} a accepte votre candidature."),
        new("MissionAssignedToProvider", "Mission affectee au prestataire", "Provider", false, true, false, true, "Nouvelle mission disponible", "Mission {Service} a accepter avant la fin du delai."),
        new("MissionQuoteSentToCustomer", "Devis mission envoye au client", "Customer", false, true, true, true, "Devis disponible", "Votre devis pour {Service} est disponible."),
        new("MissionStatusChanged", "Suivi de mission", "Mixed", true, true, false, false, "Suivi mission {NumeroMission}", "La mission {NumeroMission} a ete mise a jour.")
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
        bool PortalEnabled,
        bool MobileAppEnabled,
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
