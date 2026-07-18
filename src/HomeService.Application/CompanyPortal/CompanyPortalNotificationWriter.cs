using HomeService.Application.Abstractions;
using HomeService.Domain.Entities;

namespace HomeService.Application.CompanyPortal;

public sealed class CompanyPortalNotificationWriter(IAppDbContext db)
{
    public void AddForApplication(
        CompanyApplication application,
        string type,
        string title,
        string message,
        string tone,
        string? actionUrl = "company-profile")
    {
        if (application.CompanyId is null)
        {
            return;
        }

        db.CompanyPortalNotifications.Add(new CompanyPortalNotification(
            application.CompanyId.Value,
            application.Id,
            null,
            type,
            title,
            message,
            tone,
            actionUrl));
    }

    public void AddForDocument(
        CompanyApplication application,
        CompanyApplicationDocument document,
        string type,
        string title,
        string message,
        string tone,
        string? actionUrl = "company-profile#documents")
    {
        if (application.CompanyId is null)
        {
            return;
        }

        db.CompanyPortalNotifications.Add(new CompanyPortalNotification(
            application.CompanyId.Value,
            application.Id,
            document.Id,
            type,
            title,
            message,
            tone,
            actionUrl));
    }
}
