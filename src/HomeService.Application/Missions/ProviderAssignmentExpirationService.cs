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

        var missionIds = assignments.Select(assignment => assignment.MissionId).Distinct().ToList();
        var acceptedOffers = await db.MissionDispatchOffers
            .Where(offer => missionIds.Contains(offer.MissionId)
                && offer.Status == MissionDispatchOfferStatus.Accepted)
            .ToListAsync(cancellationToken);

        var expiredCount = 0;
        foreach (var assignment in assignments)
        {
            assignment.MarkExpired();
            assignment.Mission?.ResetDispatchAfterProviderAcceptanceTimeout(assignment.ProviderId);
            foreach (var offer in acceptedOffers.Where(offer => offer.MissionId == assignment.MissionId))
            {
                offer.MarkAssignmentTimedOut(now);
            }
            expiredCount++;
        }

        if (expiredCount > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return new ProviderAssignmentExpirationBatchResult(expiredCount);
    }

    public async Task<ProviderAssignmentExpirationBatchResult> RecoverRecentStalledAssignmentsAsync(
        DateTimeOffset createdSince,
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var missions = await db.Missions
            .Where(mission => mission.CreatedAt >= createdSince
                && mission.Status == MissionStatus.SearchingProvider
                && mission.CompanyId != null
                && mission.ProviderId == null
                && mission.CompanyAssignmentExpiresAt == null
                && db.ProviderMissionAssignments.Any(assignment =>
                    assignment.MissionId == mission.Id
                    && assignment.Status == ProviderMissionAssignmentStatus.Expired
                    && assignment.ExpiresAt <= now))
            .OrderBy(mission => mission.CreatedAt)
            .Take(Math.Clamp(batchSize, 1, 200))
            .ToListAsync(cancellationToken);

        var missionIds = missions.Select(mission => mission.Id).ToList();
        var acceptedOffers = await db.MissionDispatchOffers
            .Where(offer => missionIds.Contains(offer.MissionId)
                && offer.Status == MissionDispatchOfferStatus.Accepted)
            .ToListAsync(cancellationToken);

        foreach (var mission in missions)
        {
            mission.ResetStalledDispatch();
            foreach (var offer in acceptedOffers.Where(offer => offer.MissionId == mission.Id))
            {
                offer.MarkAssignmentTimedOut(now);
            }
        }

        if (missions.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return new ProviderAssignmentExpirationBatchResult(missions.Count);
    }
}

public sealed record ProviderAssignmentExpirationBatchResult(int ExpiredAssignmentCount);
