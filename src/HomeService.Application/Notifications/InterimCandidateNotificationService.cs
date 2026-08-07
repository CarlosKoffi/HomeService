using System.Text.Json;
using HomeService.Application.Abstractions;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Application.Notifications;

public sealed class InterimCandidateNotificationService(
    IAppDbContext db,
    MobilePushNotificationQueueService mobilePushNotifications)
{
    public async Task QueueForCompanyAsync(
        Guid companyId,
        ProviderAffiliationRequest affiliationRequest,
        string providerFullName,
        CancellationToken cancellationToken)
    {
        var title = "Nouveau prestataire à valider";
        var body = $"{providerFullName} souhaite rejoindre votre équipe comme intérimaire.";

        db.CompanyPortalNotifications.Add(new CompanyPortalNotification(
            companyId,
            null,
            null,
            "InterimCandidateReceived",
            title,
            body,
            "warning",
            "providers"));

        await mobilePushNotifications.QueueForOwnerAsync(
            MobileDeviceOwnerType.Company,
            companyId,
            title,
            body,
            nameof(ProviderAffiliationRequest),
            affiliationRequest.Id,
            JsonSerializer.Serialize(new
            {
                type = "company_provider_validation",
                providerId = affiliationRequest.ProviderId,
                requestId = affiliationRequest.Id,
                companyId
            }),
            cancellationToken,
            saveChanges: false);
    }
}
