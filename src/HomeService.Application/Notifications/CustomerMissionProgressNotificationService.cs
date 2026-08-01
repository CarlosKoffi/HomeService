using System.Text.Json;
using HomeService.Application.Abstractions;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Notifications;

public sealed class CustomerMissionProgressNotificationService(
    IAppDbContext db,
    MobilePushNotificationQueueService mobilePushNotifications,
    NotificationDeliveryPreferenceService notificationPreferences,
    NotificationTemplateService notificationTemplates)
{
    public const string CompanyAnalyzingEventKey = "MissionCompanyAnalyzing";
    public const string TechnicianProposedEventKey = "MissionTechnicianProposed";

    public async Task NotifyCompanyAnalyzingAsync(
        Mission mission,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var company = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);

        var variables = await BuildVariablesAsync(mission, company?.Name, null, cancellationToken);
        await QueueAsync(
            mission,
            CompanyAnalyzingEventKey,
            "Votre demande est en cours d'analyse",
            "{NomEntreprise} s'apprete a prendre en charge votre demande {NumeroMission}.",
            variables,
            "mission_company_analyzing",
            cancellationToken);
    }

    public async Task NotifyTechnicianProposedAsync(
        Mission mission,
        ProviderProfile provider,
        ProviderMissionAssignment assignment,
        CancellationToken cancellationToken)
    {
        var companyName = provider.Company?.Name;
        if (string.IsNullOrWhiteSpace(companyName) && mission.CompanyId is not null)
        {
            companyName = await db.Companies
                .AsNoTracking()
                .Where(item => item.Id == mission.CompanyId)
                .Select(item => item.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var variables = await BuildVariablesAsync(mission, companyName, provider.FullName, cancellationToken);
        await QueueAsync(
            mission,
            TechnicianProposedEventKey,
            "Un technicien a ete trouve",
            "Nous avons trouve un technicien pour votre mission {NumeroMission}. Nous attendons sa confirmation.",
            variables,
            "mission_technician_proposed",
            cancellationToken,
            assignment.Id);
    }

    private async Task QueueAsync(
        Mission mission,
        string eventKey,
        string fallbackSubject,
        string fallbackBody,
        IReadOnlyDictionary<string, string?> variables,
        string metadataType,
        CancellationToken cancellationToken,
        Guid? assignmentId = null)
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

        var message = await notificationTemplates.RenderAsync(
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
                companyId = mission.CompanyId,
                providerId = mission.ProviderId
            }),
            cancellationToken,
            saveChanges: false);
    }

    private async Task<IReadOnlyDictionary<string, string?>> BuildVariablesAsync(
        Mission mission,
        string? companyName,
        string? providerName,
        CancellationToken cancellationToken)
    {
        var service = await db.Services
            .AsNoTracking()
            .Include(item => item.Prestations)
            .FirstOrDefaultAsync(item => item.Id == mission.ServiceId, cancellationToken);
        var prestation = service?.Prestations.FirstOrDefault(item => item.Id == mission.ServicePrestationId);

        return NotificationTemplateRenderer.Variables(
            ("NomEntreprise", companyName),
            ("NomPrestataire", providerName),
            ("NomTechnicien", providerName),
            ("Service", service?.Name),
            ("Prestation", prestation?.Name),
            ("DescriptionService", mission.Description ?? service?.Description),
            ("NumeroMission", mission.MissionNumber),
            ("DateMission", mission.ScheduledFor?.ToLocalTime().ToString("dd/MM/yyyy HH:mm")),
            ("Adresse", mission.ServiceAddress),
            ("LienAction", $"missions/{mission.Id}"));
    }
}
