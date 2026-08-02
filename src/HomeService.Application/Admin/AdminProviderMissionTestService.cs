using HomeService.Application.Abstractions;
using HomeService.Application.ProviderPortal;
using HomeService.Contracts.Admin;
using HomeService.Contracts.ProviderPortal;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminProviderMissionTestService(
    IAppDbContext db,
    ProviderMissionWorkflowService workflow,
    ProviderMissionNotificationService notifications)
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
            .Where(item => item.Status == ProviderMissionAssignmentStatus.Offered)
            .OrderBy(item => item.ExpiresAt)
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
            var expired = item.ExpiresAt <= now;
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
                !expired && providerAllowed,
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
}
