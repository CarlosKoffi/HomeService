using HomeService.Application.Abstractions;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.ProviderPortal;

public sealed class ProviderDepartureAutomationService(
    IAppDbContext db,
    ProviderMissionNotificationService notifications)
{
    public static readonly TimeSpan DepartureGracePeriod = TimeSpan.FromMinutes(2);

    public async Task<int> MarkDueMissionsOnTheWayAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var departureThreshold = now - DepartureGracePeriod;
        var assignments = await db.ProviderMissionAssignments
            .Include(item => item.Mission)
            .Include(item => item.Provider)
            .Where(item => item.Status == ProviderMissionAssignmentStatus.Accepted
                && item.Mission != null
                && item.Mission.Status == MissionStatus.Accepted
                && item.Mission.CustomerConfirmedAt != null
                && item.Mission.CustomerConfirmedAt <= departureThreshold
                && (item.Mission.PaymentStatus == PaymentStatus.Authorized
                    || item.Mission.PaymentStatus == PaymentStatus.Paid))
            .OrderBy(item => item.Mission!.CustomerConfirmedAt)
            .Take(Math.Clamp(batchSize, 1, 200))
            .ToListAsync(cancellationToken);

        var processed = 0;
        foreach (var assignment in assignments)
        {
            if (assignment.Mission is null || assignment.Provider is null)
            {
                continue;
            }

            assignment.Mission.MarkProviderOnTheWay(assignment.ProviderId, assignment.CompanyId);
            await notifications.NotifyOnTheWayAsync(
                assignment.Mission,
                assignment.Provider,
                assignment,
                cancellationToken);
            processed++;
        }

        if (processed > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return processed;
    }
}
