using HomeService.Application.Abstractions;
using HomeService.Application.Notifications;
using HomeService.Contracts.CompanyPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace HomeService.Application.CompanyPortal;

public sealed class CompanyMissionAssignmentService(
    IAppDbContext db,
    MobilePushNotificationQueueService mobilePushNotifications,
    NotificationDeliveryPreferenceService notificationPreferences,
    NotificationTemplateService notificationTemplates)
{
    private static readonly TimeSpan AssignmentAcceptanceWindow = TimeSpan.FromMinutes(3);
    private const string MissionAssignedToProviderEventKey = "MissionAssignedToProvider";

    public async Task<CompanyAssignableProvidersResult> ListAssignableProvidersAsync(Guid companyId, Guid missionId, CancellationToken cancellationToken)
    {
        var mission = await db.Missions
            .AsNoTracking()
            .FirstOrDefaultAsync(mission => mission.Id == missionId && mission.CompanyId == companyId, cancellationToken);
        if (mission is null)
        {
            return CompanyAssignableProvidersResult.NotFound();
        }

        var busyProviderIds = await db.ProviderMissionAssignments
            .AsNoTracking()
            .Where(assignment => assignment.CompanyId == companyId
                && (assignment.Status == ProviderMissionAssignmentStatus.Offered
                    || assignment.Status == ProviderMissionAssignmentStatus.Accepted
                    || assignment.Status == ProviderMissionAssignmentStatus.Started))
            .Select(assignment => assignment.ProviderId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var unavailableForThisMissionProviderIds = await db.ProviderMissionAssignments
            .AsNoTracking()
            .Where(assignment => assignment.CompanyId == companyId
                && assignment.MissionId == missionId
                && (assignment.Status == ProviderMissionAssignmentStatus.Refused
                    || assignment.Status == ProviderMissionAssignmentStatus.Expired))
            .Select(assignment => assignment.ProviderId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var providers = await db.Providers
            .AsNoTracking()
            .Where(provider => provider.CompanyId == companyId
                && provider.Status == ProviderStatus.Approved
                && provider.Services.Any(service =>
                    service.IsActive
                    && service.ServiceId == mission.ServiceId
                    && (mission.ServicePrestationId == null
                        || service.Prestations.Any(prestation =>
                            prestation.IsActive
                            && prestation.ServicePrestationId == mission.ServicePrestationId)))
                && !busyProviderIds.Contains(provider.Id)
                && !unavailableForThisMissionProviderIds.Contains(provider.Id))
            .OrderByDescending(provider => provider.IsAvailable)
            .ThenBy(provider => provider.LastName)
            .Select(provider => new CompanyPortalAssignableProviderResponse(
                provider.Id,
                provider.FirstName + " " + provider.LastName,
                provider.PhoneNumber,
                provider.Status.ToString(),
                provider.IsAvailable,
                provider.EmploymentType.ToString(),
                provider.YearsOfExperience,
                provider.Services
                    .Where(service => service.IsActive && service.ServiceId == mission.ServiceId)
                    .Select(service => service.ExperienceLevel.ToString())
                    .FirstOrDefault() ?? "Confirmed",
                provider.Services
                    .Where(service => service.IsActive && service.ServiceId == mission.ServiceId)
                    .Select(service => service.PriceTier.ToString())
                    .FirstOrDefault() ?? "Normal",
                provider.Services
                    .Where(service => service.IsActive && service.ServiceId == mission.ServiceId)
                    .Select(service => service.Service!.NormalPriceAmount)
                    .FirstOrDefault(),
                provider.Services
                    .Where(service => service.IsActive && service.ServiceId == mission.ServiceId)
                    .Select(service => service.Service!.PremiumPriceAmount)
                    .FirstOrDefault(),
                provider.Services
                    .Where(service => service.IsActive && service.ServiceId == mission.ServiceId)
                    .Select(service => service.Service!.Currency)
                    .FirstOrDefault() ?? "XOF",
                provider.Documents.Any(document => document.DocumentType == ProviderDocumentType.Diploma),
                provider.Documents
                    .Where(document => document.DocumentType == ProviderDocumentType.Photo)
                    .OrderByDescending(document => document.CreatedAt)
                    .Select(document => $"/api/company-portal/provider-documents/{document.Id}/preview")
                    .FirstOrDefault(),
                provider.Services
                    .Where(service => service.IsActive && service.ServiceId == mission.ServiceId)
                    .Select(service => service.Service!.PriceMinAmount)
                    .FirstOrDefault(),
                provider.Services
                    .Where(service => service.IsActive && service.ServiceId == mission.ServiceId)
                    .Select(service => service.Service!.PriceMaxAmount)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return CompanyAssignableProvidersResult.Ok(providers);
    }

    public async Task<CompanyMissionAssignmentResult> AssignAsync(
        Guid companyId,
        Guid missionId,
        Guid providerId,
        int quotedAmount,
        string? overMaxJustification,
        CancellationToken cancellationToken)
    {
        var mission = await db.Missions.FirstOrDefaultAsync(mission => mission.Id == missionId && mission.CompanyId == companyId, cancellationToken);
        var provider = await db.Providers
            .Include(provider => provider.Company)
            .Include(provider => provider.Services)
                .ThenInclude(service => service.Service)
            .Include(provider => provider.Services)
                .ThenInclude(service => service.Prestations)
            .FirstOrDefaultAsync(provider => provider.Id == providerId && provider.CompanyId == companyId, cancellationToken);

        var hasBlockingAssignment = await db.ProviderMissionAssignments.AnyAsync(assignment =>
            assignment.ProviderId == providerId
            && (assignment.Status == ProviderMissionAssignmentStatus.Offered
                || assignment.Status == ProviderMissionAssignmentStatus.Accepted
                || assignment.Status == ProviderMissionAssignmentStatus.Started),
            cancellationToken);
        var alreadyUnavailableForThisMission = await db.ProviderMissionAssignments.AnyAsync(assignment =>
            assignment.ProviderId == providerId
            && assignment.MissionId == missionId
            && (assignment.Status == ProviderMissionAssignmentStatus.Refused
                || assignment.Status == ProviderMissionAssignmentStatus.Expired),
            cancellationToken);

        var providerService = mission is null || provider is null
            ? null
            : provider.Services.FirstOrDefault(service =>
                service.IsActive
                && service.ServiceId == mission.ServiceId
                && (mission.ServicePrestationId == null
                    || service.Prestations.Any(prestation =>
                        prestation.IsActive
                        && prestation.ServicePrestationId == mission.ServicePrestationId)));
        var policy = CompanyMissionAssignmentPolicy.Validate(
            mission is not null,
            provider is not null,
            provider?.Status == ProviderStatus.Approved,
            providerService is not null,
            hasBlockingAssignment,
            alreadyUnavailableForThisMission);
        if (!policy.IsValid)
        {
            return policy.IsNotFound
                ? CompanyMissionAssignmentResult.NotFound(policy.Message ?? "Element introuvable.")
                : CompanyMissionAssignmentResult.Invalid(policy.Message ?? "Affectation impossible.");
        }

        var validMission = mission!;
        var validProvider = provider!;
        var validProviderService = providerService!;
        var maxAllowedAmount = validProviderService.Service!.PriceMaxAmount;
        if (quotedAmount > maxAllowedAmount && string.IsNullOrWhiteSpace(overMaxJustification))
        {
            return CompanyMissionAssignmentResult.Invalid("Justifiez le depassement du prix maximum configure avant d'envoyer le devis au client.");
        }

        validMission.AssignWithCompanyQuote(providerId, companyId, quotedAmount, maxAllowedAmount, overMaxJustification);

        var assignment = new ProviderMissionAssignment(
            validMission.Id,
            validProvider.Id,
            companyId,
            DateTimeOffset.UtcNow.Add(AssignmentAcceptanceWindow));
        db.ProviderMissionAssignments.Add(assignment);
        db.CompanyPortalActivities.Add(new CompanyPortalActivity(
            companyId,
            "mission",
            "Mission assignee",
            $"{validProvider.FullName} a recu une mission {validProviderService.Service!.Name} avec un devis de {quotedAmount:N0} {validProviderService.Service!.Currency}.",
            "blue",
            nameof(Mission),
            validMission.Id));

        await QueueProviderMissionPushAsync(
            validMission,
            validProvider,
            validProviderService.Service!,
            assignment,
            companyId,
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return CompanyMissionAssignmentResult.Ok(new AssignCompanyMissionResponse(
            validMission.Id,
            validProvider.Id,
            assignment.Id,
            assignment.Status.ToString(),
            assignment.ExpiresAt));
    }

    private async Task QueueProviderMissionPushAsync(
        Mission mission,
        ProviderProfile provider,
        Service service,
        ProviderMissionAssignment assignment,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var preference = await notificationPreferences.GetAsync(
            MissionAssignedToProviderEventKey,
            "Provider",
            defaultEmailEnabled: false,
            defaultWhatsAppEnabled: true,
            cancellationToken);

        if (!preference.MobileAppEnabled)
        {
            return;
        }

        var variables = NotificationTemplateRenderer.Variables(
            ("NomPrestataire", provider.FullName),
            ("Service", service.Name),
            ("DescriptionService", service.Description),
            ("NumeroMission", mission.MissionNumber),
            ("NomEntreprise", provider.Company?.Name),
            ("DelaiMinutes", ((int)AssignmentAcceptanceWindow.TotalMinutes).ToString("N0")),
            ("DateExpiration", assignment.ExpiresAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm")));

        var message = await notificationTemplates.RenderAsync(
            MissionAssignedToProviderEventKey,
            NotificationTemplateChannel.MobilePush,
            "Nouvelle mission disponible",
            "Mission {Service} a accepter avant la fin du delai.",
            variables,
            cancellationToken);

        await mobilePushNotifications.QueueForOwnerAsync(
            MobileDeviceOwnerType.Provider,
            provider.Id,
            message.Subject,
            message.Body,
            nameof(ProviderMissionAssignment),
            assignment.Id,
            JsonSerializer.Serialize(new
            {
                type = "provider_mission_offer",
                missionId = mission.Id,
                missionNumber = mission.MissionNumber,
                assignmentId = assignment.Id,
                serviceId = mission.ServiceId,
                serviceName = service.Name,
                servicePrestationId = mission.ServicePrestationId,
                companyId,
                expiresAt = assignment.ExpiresAt
            }),
            cancellationToken,
            saveChanges: false);
    }
}

public sealed record CompanyAssignableProvidersResult(bool IsSuccess, IReadOnlyList<CompanyPortalAssignableProviderResponse> Providers, string? Message)
{
    public static CompanyAssignableProvidersResult Ok(IReadOnlyList<CompanyPortalAssignableProviderResponse> providers) => new(true, providers, null);
    public static CompanyAssignableProvidersResult NotFound() => new(false, [], "Mission introuvable.");
}

public sealed record CompanyMissionAssignmentResult(bool IsSuccess, AssignCompanyMissionResponse? Response, string? Message, bool IsNotFound)
{
    public static CompanyMissionAssignmentResult Ok(AssignCompanyMissionResponse response) => new(true, response, null, false);
    public static CompanyMissionAssignmentResult Invalid(string message) => new(false, null, message, false);
    public static CompanyMissionAssignmentResult NotFound(string message) => new(false, null, message, true);
}
