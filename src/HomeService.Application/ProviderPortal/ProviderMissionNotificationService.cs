using System.Text.Json;
using HomeService.Application.Abstractions;
using HomeService.Application.CompanyPortal;
using HomeService.Application.Notifications;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.ProviderPortal;

public sealed class ProviderMissionNotificationService(
    IAppDbContext db,
    CompanyPortalNotificationWriter companyNotifications,
    MobilePushNotificationQueueService mobilePushNotifications,
    NotificationDeliveryPreferenceService notificationPreferences,
    NotificationTemplateService notificationTemplates)
{
    private const string MissionProviderAcceptedEventKey = "MissionProviderAccepted";
    private const string MissionProviderRefusedEventKey = "MissionProviderRefused";
    private const string MissionPaymentRequiredEventKey = "MissionPaymentRequired";
    private const string MissionCompletedEventKey = "MissionCompleted";

    public async Task NotifyAcceptedAsync(
        Mission mission,
        ProviderProfile provider,
        ProviderMissionAssignment assignment,
        CancellationToken cancellationToken)
    {
        var variables = await BuildVariablesAsync(mission, provider, cancellationToken);
        var companyMessage = await RenderAsync(
            MissionProviderAcceptedEventKey,
            NotificationTemplateChannel.Portal,
            "{NomPrestataire} a accepte la mission {NumeroMission}",
            "{NomPrestataire} a accepte la mission {NumeroMission}.",
            variables,
            cancellationToken);

        companyNotifications.AddForMission(
            mission,
            MissionProviderAcceptedEventKey,
            companyMessage.Subject,
            companyMessage.Body,
            "success",
            $"missions/{mission.Id}");

        if (mission.CompanyId.HasValue)
        {
            await mobilePushNotifications.QueueForOwnerAsync(
                MobileDeviceOwnerType.Company,
                mission.CompanyId.Value,
                "Prestataire confirmé",
                $"{provider.FullName} a accepté la mission {mission.MissionNumber}. Le paiement client est maintenant attendu.",
                nameof(Mission),
                mission.Id,
                null,
                cancellationToken,
                saveChanges: false);
        }

        await QueueCustomerPushAsync(
            mission,
            MissionPaymentRequiredEventKey,
            "Votre paiement est attendu",
            "{NomTechnicien} a accepte la mission {NumeroMission}. Validez le prix de {Montant} et payez pour lancer l'intervention.",
            variables,
            "mission_payment_required",
            assignment.Id,
            cancellationToken);
    }

    public async Task NotifyRefusedAsync(
        Mission mission,
        ProviderProfile provider,
        ProviderMissionAssignment assignment,
        CancellationToken cancellationToken)
    {
        var variables = await BuildVariablesAsync(
            mission,
            provider,
            cancellationToken,
            ("Motif", BuildRefusalLabel(assignment)));
        var message = await RenderAsync(
            MissionProviderRefusedEventKey,
            NotificationTemplateChannel.Portal,
            "{NomPrestataire} a refuse la mission {NumeroMission}",
            "{NomPrestataire} a refuse la mission {NumeroMission}. {Motif}",
            variables,
            cancellationToken);

        companyNotifications.AddForMission(
            mission,
            MissionProviderRefusedEventKey,
            message.Subject,
            message.Body,
            "warning",
            $"missions/{mission.Id}");

        if (mission.CompanyId.HasValue)
        {
            await mobilePushNotifications.QueueForOwnerAsync(
                MobileDeviceOwnerType.Company,
                mission.CompanyId.Value,
                "Prestataire indisponible",
                $"{provider.FullName} a refusé la mission {mission.MissionNumber}. Affectez rapidement un autre prestataire.",
                nameof(Mission),
                mission.Id,
                null,
                cancellationToken,
                saveChanges: false);
        }
    }

    public Task NotifyArrivedAsync(
        Mission mission,
        ProviderProfile provider,
        ProviderMissionAssignment assignment,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task NotifyOnTheWayAsync(
        Mission mission,
        ProviderProfile provider,
        ProviderMissionAssignment assignment,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task NotifyStartedAsync(
        Mission mission,
        ProviderProfile provider,
        ProviderMissionAssignment assignment,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task NotifyCompletedAsync(
        Mission mission,
        ProviderProfile provider,
        ProviderMissionAssignment assignment,
        CancellationToken cancellationToken)
    {
        var variables = await BuildVariablesAsync(mission, provider, cancellationToken);
        await QueueCustomerPushAsync(
            mission,
            MissionCompletedEventKey,
            "Mission terminee",
            "La mission {NumeroMission} est terminee. Vous pouvez valider l'intervention.",
            variables,
            "mission_completed",
            assignment.Id,
            cancellationToken);

    }

    private async Task QueueCustomerPushAsync(
        Mission mission,
        string eventKey,
        string fallbackSubject,
        string fallbackBody,
        IReadOnlyDictionary<string, string?> variables,
        string metadataType,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var preference = await notificationPreferences.GetAsync(
            eventKey,
            "Customer",
            defaultEmailEnabled: false,
            defaultWhatsAppEnabled: false,
            cancellationToken);

        if (!preference.MobileAppEnabled)
        {
            return;
        }

        var message = await RenderAsync(
            eventKey,
            NotificationTemplateChannel.MobilePush,
            fallbackSubject,
            fallbackBody,
            variables,
            cancellationToken);

        await mobilePushNotifications.QueueForOwnerAsync(
            MobileDeviceOwnerType.Customer,
            mission.CustomerId,
            message.Subject,
            message.Body,
            nameof(Mission),
            mission.Id,
            JsonSerializer.Serialize(new
            {
                type = metadataType,
                missionId = mission.Id,
                missionNumber = mission.MissionNumber,
                assignmentId,
                providerId = mission.ProviderId,
                companyId = mission.CompanyId
            }),
            cancellationToken,
            saveChanges: false);
    }

    private async Task<RenderedNotificationTemplate> RenderAsync(
        string eventKey,
        NotificationTemplateChannel channel,
        string fallbackSubject,
        string fallbackBody,
        IReadOnlyDictionary<string, string?> variables,
        CancellationToken cancellationToken)
    {
        return await notificationTemplates.RenderAsync(
            eventKey,
            channel,
            fallbackSubject,
            fallbackBody,
            variables,
            cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, string?>> BuildVariablesAsync(
        Mission mission,
        ProviderProfile provider,
        CancellationToken cancellationToken,
        params (string Key, string? Value)[] extraVariables)
    {
        var service = await db.Services
            .AsNoTracking()
            .Include(item => item.Prestations)
            .FirstOrDefaultAsync(item => item.Id == mission.ServiceId, cancellationToken);
        var company = mission.CompanyId is null
            ? null
            : await db.Companies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == mission.CompanyId, cancellationToken);
        var customer = await db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == mission.CustomerId, cancellationToken);
        var prestation = service?.Prestations.FirstOrDefault(item => item.Id == mission.ServicePrestationId);

        return NotificationTemplateRenderer.Variables(
            new (string Key, string? Value)[]
            {
                ("NomEntreprise", company?.Name ?? provider.Company?.Name),
                ("NomPrestataire", provider.FullName),
                ("NomTechnicien", provider.FullName),
                ("NomClient", BuildCustomerName(customer)),
                ("Service", service?.Name),
                ("Prestation", prestation?.Name),
                ("DescriptionService", mission.Description ?? service?.Description),
                ("NumeroMission", mission.MissionNumber),
                ("Montant", mission.FinalTotalAmount?.ToString("N0") ?? mission.CompanyQuotedAmount?.ToString("N0") ?? mission.EstimatedTotalAmount?.ToString("N0")),
                ("DateMission", mission.ScheduledFor?.ToLocalTime().ToString("dd/MM/yyyy HH:mm")),
                ("Adresse", mission.ServiceAddress),
                ("LienAction", $"missions/{mission.Id}")
            }.Concat(extraVariables).ToArray());
    }

    private static string? BuildCustomerName(CustomerProfile? customer)
    {
        if (customer is null)
        {
            return null;
        }

        var name = $"{customer.FirstName} {customer.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? "Client" : name;
    }

    private static string BuildRefusalLabel(ProviderMissionAssignment assignment)
    {
        var reason = assignment.RefusalReason?.ToString() ?? "Non renseignee";
        return string.IsNullOrWhiteSpace(assignment.RefusalComment)
            ? reason
            : $"{reason} - {assignment.RefusalComment}";
    }
}
