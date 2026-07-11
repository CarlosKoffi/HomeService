using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Application.Notifications;

public static class CompanyApplicationNotificationFactory
{
    public static IReadOnlyList<NotificationOutboxMessage> CreateApplicantNotifications(
        CompanyApplication application,
        string subject,
        string body,
        bool includeWhatsApp,
        string? metadataJson = null)
    {
        var messages = new List<NotificationOutboxMessage>();

        if (!string.IsNullOrWhiteSpace(application.Email))
        {
            messages.Add(new NotificationOutboxMessage(
                NotificationChannel.Email,
                application.Email,
                subject,
                body,
                nameof(CompanyApplication),
                application.Id,
                metadataJson));
        }

        if (includeWhatsApp && !string.IsNullOrWhiteSpace(application.PhoneNumber))
        {
            messages.Add(new NotificationOutboxMessage(
                NotificationChannel.WhatsApp,
                application.PhoneNumber,
                subject,
                body,
                nameof(CompanyApplication),
                application.Id,
                metadataJson));
        }

        return messages;
    }

    public static IReadOnlyList<NotificationOutboxMessage> Rejected(CompanyApplication application, string note)
    {
        return CreateApplicantNotifications(
            application,
            "Votre dossier entreprise a ete refuse",
            $"Votre demande d'inscription entreprise a ete refusee. Motif: {note}",
            includeWhatsApp: true);
    }

    public static IReadOnlyList<NotificationOutboxMessage> Reopened(CompanyApplication application, string note)
    {
        return CreateApplicantNotifications(
            application,
            "Votre dossier entreprise est reouvert",
            $"Votre dossier entreprise a ete reouvert pour une nouvelle verification. Note: {note}",
            includeWhatsApp: true);
    }

    public static IReadOnlyList<NotificationOutboxMessage> MoreInformationRequested(CompanyApplication application, string note)
    {
        return CreateApplicantNotifications(
            application,
            "Complement requis pour votre dossier entreprise",
            $"Notre equipe demande un complement sur votre dossier entreprise. Detail: {note}",
            includeWhatsApp: true);
    }
}
