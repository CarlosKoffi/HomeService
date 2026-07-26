using HomeService.Application.Abstractions;
using HomeService.Application.Notifications;
using HomeService.Contracts.Notifications;
using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminNotificationTemplateService(IAppDbContext db)
{
    public async Task<IReadOnlyList<NotificationTemplateResponse>> ListAsync(CancellationToken cancellationToken)
    {
        await EnsureDefaultsAsync(cancellationToken);

        return await db.NotificationTemplates
            .AsNoTracking()
            .OrderBy(template => template.Audience)
            .ThenBy(template => template.EventKey)
            .ThenBy(template => template.Channel)
            .Select(template => ToResponse(template))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminNotificationTemplateResult> UpdateAsync(
        Guid templateId,
        UpdateNotificationTemplateRequest request,
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

        template.Update(
            request.Label,
            request.Audience,
            request.SubjectTemplate,
            request.BodyTemplate,
            request.AvailableVariables,
            request.IsActive);

        return AdminNotificationTemplateResult.Ok(ToResponse(template));
    }

    private async Task EnsureDefaultsAsync(CancellationToken cancellationToken)
    {
        var existing = await db.NotificationTemplates
            .Select(template => new { template.EventKey, template.Channel })
            .ToListAsync(cancellationToken);
        var existingKeys = existing
            .Select(template => $"{template.EventKey}|{template.Channel}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var hasAdded = false;
        foreach (var seed in NotificationTemplateCatalog.Defaults)
        {
            foreach (var channel in seed.Channels)
            {
                var key = $"{seed.EventKey}|{channel}";
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
                hasAdded = true;
            }
        }

        if (hasAdded)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
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

    private static NotificationTemplateResponse ToResponse(NotificationTemplate template)
    {
        return new NotificationTemplateResponse(
            template.Id,
            template.EventKey,
            template.Channel.ToString(),
            template.Label,
            template.Audience,
            template.SubjectTemplate,
            template.BodyTemplate,
            template.AvailableVariables,
            template.IsActive,
            template.CreatedAt,
            template.UpdatedAt);
    }
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
}

public enum AdminNotificationTemplateStatus
{
    Ok,
    NotFound,
    ValidationFailed
}
