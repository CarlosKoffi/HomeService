using HomeService.Application.Abstractions;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Notifications;

public sealed class NotificationTemplateService(IAppDbContext db)
{
    public async Task<RenderedNotificationTemplate> RenderAsync(
        string eventKey,
        NotificationTemplateChannel channel,
        string fallbackSubject,
        string fallbackBody,
        IReadOnlyDictionary<string, string?> variables,
        CancellationToken cancellationToken)
    {
        var template = await db.NotificationTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.EventKey == eventKey
                    && item.Channel == channel
                    && item.IsActive,
                cancellationToken);

        var subject = NotificationTemplateRenderer.Render(
            template?.SubjectTemplate,
            fallbackSubject,
            variables);
        var body = NotificationTemplateRenderer.Render(
            template?.BodyTemplate,
            fallbackBody,
            variables);

        return new RenderedNotificationTemplate(subject, body);
    }
}

public sealed record RenderedNotificationTemplate(string Subject, string Body);
