using HomeService.Application.Abstractions;
using HomeService.Application.Auditing;
using HomeService.Application.Notifications;
using HomeService.Contracts.Notifications;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminNotificationTemplateService(IAppDbContext db, NotificationCatalogSeeder? catalogSeeder = null)
{
    public async Task<IReadOnlyList<NotificationTemplateResponse>> ListAsync(CancellationToken cancellationToken)
    {
        await (catalogSeeder ?? new NotificationCatalogSeeder(db)).EnsureDefaultsAsync(cancellationToken);

        return await db.NotificationTemplates
            .AsNoTracking()
            .OrderBy(template => template.Audience)
            .ThenBy(template => template.EventKey)
            .ThenBy(template => template.Channel)
            .Select(template => ToResponse(template))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminNotificationTemplateResult> CreateAsync(
        CreateNotificationTemplateRequest request,
        CancellationToken cancellationToken)
        => await CreateAsync(request, null, null, cancellationToken);

    public async Task<AdminNotificationTemplateResult> CreateAsync(
        CreateNotificationTemplateRequest request,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var validation = Validate(request);
        if (validation is not null)
        {
            return AdminNotificationTemplateResult.ValidationFailed(validation);
        }

        var channel = Enum.Parse<NotificationTemplateChannel>(request.Channel.Trim(), ignoreCase: true);
        var eventKey = request.EventKey.Trim();
        var exists = await db.NotificationTemplates
            .AnyAsync(item => item.EventKey == eventKey && item.Channel == channel, cancellationToken);

        if (exists)
        {
            return AdminNotificationTemplateResult.Conflict("Un modele existe deja pour cet evenement et ce canal.");
        }

        var rule = await db.NotificationDeliveryRules
            .FirstOrDefaultAsync(item => item.EventKey == eventKey, cancellationToken);
        if (rule is null)
        {
            var normalized = NormalizeChannels(request.Audience);
            db.NotificationDeliveryRules.Add(new NotificationDeliveryRule(
                eventKey,
                request.Label,
                request.Audience,
                normalized.PortalEnabled,
                normalized.MobileAppEnabled,
                channel == NotificationTemplateChannel.Email,
                channel == NotificationTemplateChannel.WhatsApp,
                request.SubjectTemplate,
                request.BodyTemplate));
        }

        var template = new NotificationTemplate(
            eventKey,
            channel,
            request.Label,
            request.Audience,
            request.SubjectTemplate,
            request.BodyTemplate,
            request.AvailableVariables);

        template.Update(
            request.Label,
            request.Audience,
            request.SubjectTemplate,
            request.BodyTemplate,
            request.AvailableVariables,
            request.IsActive);

        db.NotificationTemplates.Add(template);
        var response = ToResponse(template);
        AddAuditLog(
            actor,
            auditContext,
            "AdminNotificationTemplateCreated",
            template.Id,
            "Modele de notification cree.",
            before: null,
            after: response);
        await db.SaveChangesAsync(cancellationToken);

        return AdminNotificationTemplateResult.Ok(response);
    }

    public async Task<AdminNotificationTemplateResult> UpdateAsync(
        Guid templateId,
        UpdateNotificationTemplateRequest request,
        CancellationToken cancellationToken)
        => await UpdateAsync(templateId, request, null, null, cancellationToken);

    public async Task<AdminNotificationTemplateResult> UpdateAsync(
        Guid templateId,
        UpdateNotificationTemplateRequest request,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var template = await db.NotificationTemplates
            .FirstOrDefaultAsync(item => item.Id == templateId, cancellationToken);
        if (template is null)
        {
            return AdminNotificationTemplateResult.NotFound();
        }

        var validation = Validate(request);
        if (validation is not null)
        {
            return AdminNotificationTemplateResult.ValidationFailed(validation);
        }

        var before = ToResponse(template);
        template.Update(
            request.Label,
            request.Audience,
            request.SubjectTemplate,
            request.BodyTemplate,
            request.AvailableVariables,
            request.IsActive);

        var response = ToResponse(template);
        AddAuditLog(
            actor,
            auditContext,
            "AdminNotificationTemplateUpdated",
            template.Id,
            "Modele de notification modifie.",
            before,
            response);
        await db.SaveChangesAsync(cancellationToken);

        return AdminNotificationTemplateResult.Ok(response);
    }

    private static string? Validate(UpdateNotificationTemplateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Label))
        {
            return "Le libelle est obligatoire.";
        }

        if (string.IsNullOrWhiteSpace(request.Audience))
        {
            return "L'audience est obligatoire.";
        }

        if (string.IsNullOrWhiteSpace(request.SubjectTemplate))
        {
            return "Le sujet est obligatoire.";
        }

        if (string.IsNullOrWhiteSpace(request.BodyTemplate))
        {
            return "Le message est obligatoire.";
        }

        return null;
    }

    private static NotificationTemplateRuleChannels NormalizeChannels(string audience)
    {
        var normalized = string.IsNullOrWhiteSpace(audience) ? "Mixed" : audience.Trim();
        return new NotificationTemplateRuleChannels(
            normalized is "Company" or "Mixed",
            normalized is "Provider" or "Customer" or "Mixed");
    }

    private static string? Validate(CreateNotificationTemplateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EventKey))
        {
            return "La cle evenement est obligatoire.";
        }

        if (request.EventKey.Trim().Length > 96)
        {
            return "La cle evenement est trop longue.";
        }

        if (!Enum.TryParse<NotificationTemplateChannel>(request.Channel, ignoreCase: true, out _))
        {
            return "Canal invalide. Utilisez Portal, MobilePush, Email ou WhatsApp.";
        }

        return Validate(new UpdateNotificationTemplateRequest(
            request.Label,
            request.Audience,
            request.SubjectTemplate,
            request.BodyTemplate,
            request.AvailableVariables,
            request.IsActive));
    }

    private static NotificationTemplateResponse ToResponse(NotificationTemplate template)
    {
        return new NotificationTemplateResponse(
            template.Id,
            template.EventKey,
            template.Channel.ToString(),
            NotificationAdminGrouping.EventGroup(template.EventKey),
            NotificationAdminGrouping.ChannelGroup(template.Channel),
            template.Label,
            template.Audience,
            NotificationAdminGrouping.AudienceGroup(template.Audience),
            template.SubjectTemplate,
            template.BodyTemplate,
            template.AvailableVariables,
            template.IsActive,
            template.CreatedAt,
            template.UpdatedAt);
    }

    private void AddAuditLog(
        AuditActor? actor,
        AuditRequestContext? auditContext,
        string action,
        Guid templateId,
        string summary,
        NotificationTemplateResponse? before,
        NotificationTemplateResponse after)
    {
        if (actor is null)
        {
            return;
        }

        db.AuditLogEntries.Add(AuditLogFactory.Create(
            actor,
            action,
            nameof(NotificationTemplate),
            templateId,
            summary,
            auditContext,
            before,
            after));
    }

    private sealed record NotificationTemplateRuleChannels(bool PortalEnabled, bool MobileAppEnabled);
}

public sealed record AdminNotificationTemplateResult(
    AdminNotificationTemplateStatus Status,
    NotificationTemplateResponse? Response,
    string? Message)
{
    public static AdminNotificationTemplateResult Ok(NotificationTemplateResponse response)
        => new(AdminNotificationTemplateStatus.Ok, response, null);

    public static AdminNotificationTemplateResult NotFound()
        => new(AdminNotificationTemplateStatus.NotFound, null, "Le modele de notification n'existe plus.");

    public static AdminNotificationTemplateResult ValidationFailed(string message)
        => new(AdminNotificationTemplateStatus.ValidationFailed, null, message);

    public static AdminNotificationTemplateResult Conflict(string message)
        => new(AdminNotificationTemplateStatus.Conflict, null, message);
}

public enum AdminNotificationTemplateStatus
{
    Ok,
    NotFound,
    ValidationFailed,
    Conflict
}
