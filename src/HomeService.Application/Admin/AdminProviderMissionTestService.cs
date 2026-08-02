using HomeService.Application.Abstractions;
using HomeService.Application.Missions;
using HomeService.Application.ProviderPortal;
using HomeService.Contracts.Admin;
using HomeService.Contracts.ProviderPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminProviderMissionTestService(
    IAppDbContext db,
    ProviderMissionWorkflowService workflow,
    ProviderMissionNotificationService notifications,
    MissionPaymentMilestoneService paymentMilestones)
{
    public async Task<AdminProviderMissionTestListResponse> GetPendingAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var assignments = await db.ProviderMissionAssignments
            .AsNoTracking()
            .Include(item => item.Mission)!
                .ThenInclude(mission => mission.ServicePrestation)
            .Include(item => item.Mission)!
                .ThenInclude(mission => mission.ServiceOption)
            .Include(item => item.Provider)!
                .ThenInclude(provider => provider.Company)
            .Include(item => item.Company)
            .Where(item => item.Status == ProviderMissionAssignmentStatus.Offered
                || item.Status == ProviderMissionAssignmentStatus.Accepted
                || item.Status == ProviderMissionAssignmentStatus.Started)
            .OrderByDescending(item => item.Status)
            .ThenBy(item => item.ExpiresAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var serviceIds = assignments
            .Where(item => item.Mission is not null)
            .Select(item => item.Mission!.ServiceId)
            .Distinct()
            .ToArray();
        var serviceNames = await db.Services
            .AsNoTracking()
            .Where(service => serviceIds.Contains(service.Id))
            .ToDictionaryAsync(service => service.Id, service => service.Name, cancellationToken);

        var rows = assignments.Select(item =>
        {
            var mission = item.Mission!;
            var provider = item.Provider;
            var expired = item.Status == ProviderMissionAssignmentStatus.Offered && item.ExpiresAt <= now;
            var providerAllowed = provider is not null && ProviderMissionWorkflowService.CanProviderUsePortal(provider);
            var reason = expired
                ? "Le delai de reponse est expire. Attendez le prochain tour d'affectation."
                : providerAllowed
                    ? null
                    : "Le prestataire ou son entreprise n'est pas encore valide.";
            var label = mission.ServiceOption?.Name
                ?? mission.ServicePrestation?.Name
                ?? serviceNames.GetValueOrDefault(mission.ServiceId)
                ?? "Service";

            return new AdminProviderMissionTestAssignmentResponse(
                item.Id,
                mission.Id,
                mission.MissionNumber,
                label,
                provider?.FullName ?? "Prestataire introuvable",
                item.Company?.Name ?? provider?.Company?.Name ?? "Entreprise introuvable",
                mission.ServiceAddress ?? "Adresse non renseignee",
                item.ExpiresAt,
                GetStatusLabel(item.Status),
                item.Status == ProviderMissionAssignmentStatus.Offered && !expired && providerAllowed,
                item.Status == ProviderMissionAssignmentStatus.Accepted && providerAllowed,
                item.Status == ProviderMissionAssignmentStatus.Started && providerAllowed,
                reason);
        }).ToArray();

        return new AdminProviderMissionTestListResponse(now, rows);
    }

    public async Task<AdminProviderMissionTestActionResponse> AcceptAsync(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var assignment = await db.ProviderMissionAssignments
            .Include(item => item.Mission)
            .Include(item => item.Provider)!
                .ThenInclude(provider => provider.Company)
            .FirstOrDefaultAsync(item => item.Id == assignmentId, cancellationToken);

        if (assignment?.Mission is null || assignment.Provider is null)
        {
            return new AdminProviderMissionTestActionResponse(false, "Affectation ou prestataire introuvable.");
        }

        var previousStatus = assignment.Status;
        var latitude = assignment.Mission.ServiceLatitude ?? assignment.Provider.MissionLatitude ?? 5.3488m;
        var longitude = assignment.Mission.ServiceLongitude ?? assignment.Provider.MissionLongitude ?? -4.0031m;
        var result = workflow.AcceptMission(
            assignment.Provider,
            assignment,
            new ProviderAcceptMissionRequest(latitude, longitude, 20));

        if (result.Status != ProviderMissionOperationStatus.Ok)
        {
            return new AdminProviderMissionTestActionResponse(false, result.Message ?? "La mission ne peut pas etre acceptee.");
        }

        if (previousStatus != ProviderMissionAssignmentStatus.Accepted)
        {
            await notifications.NotifyAcceptedAsync(assignment.Mission, assignment.Provider, assignment, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return new AdminProviderMissionTestActionResponse(
            true,
            $"Mission {assignment.Mission.MissionNumber} acceptee comme {assignment.Provider.FullName}.");
    }

    public async Task<AdminProviderMissionTestActionResponse> StartAsync(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var assignment = await LoadAssignmentAsync(assignmentId, cancellationToken);
        if (assignment?.Mission is null || assignment.Provider is null)
        {
            return NotFound();
        }

        var mission = assignment.Mission;
        await RestoreMissionLocationFromCustomerAddressAsync(mission, cancellationToken);
        var previousStatus = assignment.Status;
        var latitude = mission.ServiceLatitude ?? assignment.Provider.MissionLatitude ?? 5.3488m;
        var longitude = mission.ServiceLongitude ?? assignment.Provider.MissionLongitude ?? -4.0031m;
        var result = workflow.StartMission(
            assignment.Provider,
            assignment,
            new ProviderLocationVerificationRequest(latitude, longitude, 20));

        if (result.Status != ProviderMissionOperationStatus.Ok)
        {
            return Failure(result.Message, "La mission ne peut pas demarrer.");
        }

        if (previousStatus != ProviderMissionAssignmentStatus.Started)
        {
            await paymentMilestones.EnsureMissionStartedMilestoneAsync(mission, cancellationToken);
            await notifications.NotifyStartedAsync(mission, assignment.Provider, assignment, cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);
        return Success(mission, "demarree");
    }

    public async Task<AdminProviderMissionTestActionResponse> CompleteAsync(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var assignment = await LoadAssignmentAsync(assignmentId, cancellationToken);
        if (assignment?.Mission is null || assignment.Provider is null)
        {
            return NotFound();
        }

        var mission = assignment.Mission;
        var previousStatus = assignment.Status;
        var result = workflow.CompleteMission(
            assignment.Provider,
            assignment,
            new ProviderCompleteMissionRequest(60, "Mission terminee depuis la page de test.", null));

        if (result.Status != ProviderMissionOperationStatus.Ok)
        {
            return Failure(result.Message, "La mission ne peut pas etre terminee.");
        }

        if (previousStatus != ProviderMissionAssignmentStatus.Completed)
        {
            await paymentMilestones.EnsureMissionCompletedMilestoneAsync(mission, cancellationToken);
            await notifications.NotifyCompletedAsync(mission, assignment.Provider, assignment, cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);
        return Success(mission, "terminee");
    }

    private async Task<ProviderMissionAssignment?> LoadAssignmentAsync(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var assignment = await db.ProviderMissionAssignments
            .Include(item => item.Mission)
            .Include(item => item.Provider)!
                .ThenInclude(provider => provider.Company)
            .FirstOrDefaultAsync(item => item.Id == assignmentId, cancellationToken);
        return assignment;
    }

    private async Task RestoreMissionLocationFromCustomerAddressAsync(
        Mission mission,
        CancellationToken cancellationToken)
    {
        if (mission.ServiceLatitude.HasValue && mission.ServiceLongitude.HasValue)
        {
            return;
        }

        var missionAddress = NormalizeAddress(mission.ServiceAddress);
        if (missionAddress.Length == 0)
        {
            return;
        }

        var candidates = await db.CustomerAddresses
            .AsNoTracking()
            .Where(item => item.CustomerId == mission.CustomerId
                && item.Latitude.HasValue
                && item.Longitude.HasValue)
            .ToListAsync(cancellationToken);

        var matches = candidates
            .Where(item => NormalizeAddress(item.AddressLine) == missionAddress)
            .ToArray();

        if (matches.Length == 1)
        {
            mission.SetServiceLocation(
                mission.ServiceAddress,
                matches[0].Latitude,
                matches[0].Longitude);
        }
    }

    private static string NormalizeAddress(string? value) =>
        string.Concat((value ?? string.Empty)
            .Where(character => char.IsLetterOrDigit(character)))
            .ToUpperInvariant();

    private static AdminProviderMissionTestActionResponse NotFound() =>
        new(false, "Affectation ou prestataire introuvable.");

    private static AdminProviderMissionTestActionResponse Failure(string? message, string fallback) =>
        new(false, message ?? fallback);

    private static AdminProviderMissionTestActionResponse Success(Mission mission, string action) =>
        new(true, $"Mission {mission.MissionNumber} {action}.");

    private static string GetStatusLabel(ProviderMissionAssignmentStatus status) => status switch
    {
        ProviderMissionAssignmentStatus.Offered => "En attente d'acceptation",
        ProviderMissionAssignmentStatus.Accepted => "Acceptee - prete a demarrer",
        ProviderMissionAssignmentStatus.Started => "En cours",
        _ => status.ToString()
    };
}
