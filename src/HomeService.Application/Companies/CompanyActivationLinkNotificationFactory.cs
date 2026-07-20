using HomeService.Application.Notifications;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Application.Companies;

public static class CompanyActivationLinkNotificationFactory
{
    public static IReadOnlyList<NotificationOutboxMessage> Create(
        CompanyApplication application,
        string activationLink,
        DateTimeOffset expiresAt,
        NotificationDeliveryPreference preference)
    {
        var messages = new List<NotificationOutboxMessage>();
        var metadataJson = $$"""{"activationLink":"{{activationLink}}"}""";
        var subject = "Votre portail entreprise est pret";
        var body = $"Votre dossier est valide. Creez votre mot de passe avec ce lien valable jusqu'au {expiresAt:dd/MM/yyyy HH:mm} UTC.";

        if (preference.EmailEnabled && !string.IsNullOrWhiteSpace(application.Email))
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

        if (preference.WhatsAppEnabled && !string.IsNullOrWhiteSpace(application.PhoneNumber))
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
}
