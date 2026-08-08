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
    private const string MissionTechnicianOnTheWayEventKey = "MissionTechnicianOnTheWay";
    private const string MissionTechnicianArrivedEventKey = "MissionTechnicianArrived";
    private const string MissionStartedEventKey = "MissionStarted";
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
                JsonSerializer.Serialize(new
                {
                    type = "company_provider_accepted",
                    missionId = mission.Id,
                    missionNumber = mission.MissionNumber,
                    assignmentId = assignment.Id,
                    providerId = provider.Id,
                    companyId = mission.CompanyId.Value
                }),
                cancellationToken,
                saveChanges: false);
        }

        await QueueCustomerPushAsync(
            mission,
            MissionPaymentRequiredEventKey,
            "Votre prestataire est pret",
            "{NomTechnicien} est pret pour la mission {NumeroMission}. Verifiez le montant de {Montant} et payez pour lancer l'intervention.",
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
                JsonSerializer.Serialize(new
                {
                    type = "company_provider_refused_reassign",
                    missionId = mission.Id,
                    missionNumber = mission.MissionNumber,
                    assignmentId = assignment.Id,
                    providerId = provider.Id,
                    companyId = mission.CompanyId.Value
                }),
                cancellationToken,
                saveChanges: false);
        }
    }

    public async Task NotifyArrivedAsync(
        Mission mission,
        ProviderProfile provider,
        ProviderMissionAssignment assignment,
        CancellationToken cancellationToken)
    {
        var variables = await BuildVariablesAsync(mission, provider, cancellationToken);
        await QueueCustomerPushAsync(
            mission,
            MissionTechnicianArrivedEventKey,
            "Votre prestataire est arrivé",
            "{NomTechnicien} est arrivé pour la mission {NumeroMission}.",
            variables,
            "mission_technician_arrived",
            assignment.Id,
            cancellationToken);
        await QueueCompanyStatusPushAsync(
            mission,
            provider,
            assignment,
            "MissionTechnicianArrivedCompany",
            "Prestataire arrivé",
            $"{provider.FullName} est arrivé pour la mission {mission.MissionNumber}.",
            "company_provider_arrived",
            cancellationToken);
    }

    public async Task NotifyOnTheWayAsync(
        Mission mission,
        ProviderProfile provider,
        ProviderMissionAssignment assignment,
        CancellationToken cancellationToken)
    {
        var variables = await BuildVariablesAsync(mission, provider, cancellationToken);
        await QueueCustomerPushAsync(
            mission,
            MissionTechnicianOnTheWayEventKey,
            "Votre prestataire est en route",
            "{NomTechnicien} est en route vers {Adresse} pour la mission {NumeroMission}.",
            variables,
            "mission_technician_on_the_way",
            assignment.Id,
            cancellationToken);
        await QueueCompanyStatusPushAsync(
            mission,
            provider,
            assignment,
            "MissionProviderOnTheWayCompany",
            "Prestataire en route",
            $"{provider.FullName} est en route pour la mission {mission.MissionNumber}.",
            "company_provider_on_the_way",
            cancellationToken);
    }

    public async Task NotifyStartedAsync(
        Mission mission,
        ProviderProfile provider,
        ProviderMissionAssignment assignment,
        CancellationToken cancellationToken)
    {
        var variables = await BuildVariablesAsync(mission, provider, cancellationToken);
        await QueueCustomerPushAsync(
            mission,
            MissionStartedEventKey,
            "Votre intervention a démarré",
            "La mission {NumeroMission} a démarré.",
            variables,
            "mission_started",
            assignment.Id,
            cancellationToken);
        await QueueCompanyStatusPushAsync(
            mission,
            provider,
            assignment,
            "MissionStartedCompany",
            "Mission démarrée",
            $"{provider.FullName} a démarré la mission {mission.MissionNumber}.",
            "company_mission_started",
            cancellationToken);
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
        await QueueCompanyStatusPushAsync(
            mission,
            provider,
            assignment,
            "MissionCompletedCompany",
            "Mission terminée",
            $"La mission {mission.MissionNumber} est terminée et attend la validation du client.",
            "company_mission_completed",
            cancellationToken);
    }

    private async Task QueueCompanyStatusPushAsync(
        Mission mission,
        ProviderProfile provider,
        ProviderMissionAssignment assignment,
        string eventKey,
        string title,
        string body,
        string metadataType,
        CancellationToken cancellationToken)
    {
        if (!mission.CompanyId.HasValue)
        {
            return;
        }

        companyNotifications.AddForMission(
            mission,
            eventKey,
            title,
            body,
            "info",
            $"missions/{mission.Id}");

        await mobilePushNotifications.QueueForOwnerAsync(
            MobileDeviceOwnerType.Company,
            mission.CompanyId.Value,
            title,
            body,
            nameof(Mission),
            mission.Id,
            JsonSerializer.Serialize(new
            {
                type = metadataType,
                missionId = mission.Id,
                missionNumber = mission.MissionNumber,
                assignmentId = assignment.Id,
                providerId = provider.Id,
                companyId = mission.CompanyId.Value
            }),
            cancellationToken,
            saveChanges: false);
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
