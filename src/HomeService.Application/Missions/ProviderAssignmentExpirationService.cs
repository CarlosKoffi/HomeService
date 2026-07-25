using HomeService.Application.Abstractions;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Missions;

public sealed class ProviderAssignmentExpirationService(IAppDbContext db)
{
    public async Task<ProviderAssignmentExpirationBatchResult> ExpireDueAssignmentsAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var assignments = await db.ProviderMissionAssignments
            .Include(assignment => assignment.Mission)
            .Where(assignment => assignment.Status == ProviderMissionAssignmentStatus.Offered
                && assignment.ExpiresAt <= now)
            .OrderBy(assignment => assignment.ExpiresAt)
            .Take(Math.Clamp(batchSize, 1, 200))
            .ToListAsync(cancellationToken);

        var expiredCount = 0;
        foreach (var assignment in assignments)
        {
            assignment.MarkExpired();
            expiredCount++;
        }

        if (expiredCount > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return new ProviderAssignmentExpirationBatchResult(expiredCount);
    }
}

public sealed record ProviderAssignmentExpirationBatchResult(int ExpiredAssignmentCount);
