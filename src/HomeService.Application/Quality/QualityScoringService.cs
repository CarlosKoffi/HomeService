using HomeService.Application.Abstractions;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Quality;

public sealed class QualityScoringService(IAppDbContext db)
{
    public async Task EnsureCompletionAuditAndScoresAsync(
        Mission mission,
        ProviderMissionAssignment assignment,
        CancellationToken cancellationToken)
    {
        var completedCount = await CountCompletedMissionsAsync(
            assignment.ProviderId,
            mission.ServiceId,
            mission.ServicePrestationId,
            cancellationToken);

        if (ShouldAudit(mission.Id, completedCount)
            && !await db.MissionQualityAudits.AnyAsync(item => item.MissionId == mission.Id, cancellationToken))
        {
            db.MissionQualityAudits.Add(new MissionQualityAudit(
                mission.Id,
                assignment.ProviderId,
                assignment.CompanyId,
                mission.ServiceId,
                mission.ServicePrestationId,
                completedCount <= 5
                    ? "Premieres missions - controle systematique"
                    : completedCount <= 20
                        ? "Periode de progression - controle aleatoire"
                        : "Controle qualite aleatoire"));
        }

        await RecalculateProviderAsync(assignment.ProviderId, mission.ServiceId, mission.ServicePrestationId, cancellationToken);
        await RecalculateCompanyAsync(assignment.CompanyId, mission.ServiceId, mission.ServicePrestationId, cancellationToken);
    }

    public async Task RecalculateProviderAsync(
        Guid providerId,
        Guid serviceId,
        Guid? servicePrestationId,
        CancellationToken cancellationToken)
    {
        var missions = await (from assignment in db.ProviderMissionAssignments.AsNoTracking()
                              join mission in db.Missions.AsNoTracking() on assignment.MissionId equals mission.Id
                              where assignment.ProviderId == providerId
                                  && mission.ServiceId == serviceId
                                  && mission.ServicePrestationId == servicePrestationId
                                  && (mission.Status == MissionStatus.Completed || mission.Status == MissionStatus.Resolved)
                              select new
                              {
                                  mission.Id,
                                  mission.ScheduledFor,
                                  assignment.StartedAt
                              }).ToListAsync(cancellationToken);

        var missionIds = missions.Select(item => item.Id).ToList();
        var ratings = missionIds.Count == 0
            ? []
            : await db.MissionReviews.AsNoTracking()
                .Where(item => item.ProviderId == providerId && missionIds.Contains(item.MissionId))
                .Select(item => item.OverallRating)
                .ToListAsync(cancellationToken);
        var audits = missionIds.Count == 0
            ? []
            : await db.MissionQualityAudits.AsNoTracking()
                .Where(item => item.ProviderId == providerId && missionIds.Contains(item.MissionId))
                .Select(item => new { item.Status, item.Score })
                .ToListAsync(cancellationToken);
        var failedAudits = audits.Count(item => item.Status == QualityAuditStatus.Failed);
        var decidedAudits = audits.Count(item => item.Status is QualityAuditStatus.Passed or QualityAuditStatus.Failed);
        var passedAudits = audits.Count(item => item.Status == QualityAuditStatus.Passed);
        var averageRating = ratings.Count == 0 ? 0m : (decimal)ratings.Average();

        var scheduled = missions.Where(item => item.ScheduledFor.HasValue && item.StartedAt.HasValue).ToList();
        var punctualityRate = scheduled.Count == 0
            ? 100m
            : Math.Round(100m * scheduled.Count(item => item.StartedAt <= item.ScheduledFor!.Value.AddMinutes(15)) / scheduled.Count, 2);
        var ratingPoints = ratings.Count == 0 ? 24.5m : averageRating / 5m * 35m;
        var auditPoints = decidedAudits == 0 ? 21m : 30m * passedAudits / decidedAudits;
        var punctualityPoints = punctualityRate / 100m * 15m;
        var workflowPoints = 10m;
        var incidentPoints = Math.Max(0m, 10m - failedAudits * 3m);
        var score = (int)Math.Round(ratingPoints + auditPoints + punctualityPoints + workflowPoints + incidentPoints);
        var level = ResolveLevel(score, missions.Count, failedAudits);

        var summary = await db.ProviderQualitySummaries.FirstOrDefaultAsync(item =>
            item.ProviderId == providerId
            && item.ServiceId == serviceId
            && item.ServicePrestationId == servicePrestationId,
            cancellationToken);
        if (summary is null)
        {
            summary = new ProviderQualitySummary(providerId, serviceId, servicePrestationId);
            db.ProviderQualitySummaries.Add(summary);
        }

        summary.Recalculate(
            score,
            level,
            missions.Count,
            decidedAudits,
            passedAudits,
            failedAudits,
            averageRating,
            punctualityRate);
    }

    public async Task RecalculateCompanyAsync(
        Guid companyId,
        Guid serviceId,
        Guid? servicePrestationId,
        CancellationToken cancellationToken)
    {
        var providerScores = await db.ProviderQualitySummaries
            .AsNoTracking()
            .Where(item => item.Provider!.CompanyId == companyId
                && item.ServiceId == serviceId
                && item.ServicePrestationId == servicePrestationId)
            .ToListAsync(cancellationToken);
        var completedMissionCount = providerScores.Sum(item => item.CompletedMissionCount);
        var weightedScore = completedMissionCount == 0
            ? 70
            : (int)Math.Round(providerScores.Sum(item => item.Score * item.CompletedMissionCount) / (decimal)completedMissionCount);
        var weightedRating = completedMissionCount == 0
            ? 0m
            : providerScores.Sum(item => item.AverageRating * item.CompletedMissionCount) / completedMissionCount;
        var decidedAudits = providerScores.Sum(item => item.AuditedMissionCount);
        var passedAudits = providerScores.Sum(item => item.PassedAuditCount);
        var auditPassRate = decidedAudits == 0 ? 100m : Math.Round(100m * passedAudits / decidedAudits, 2);
        var eligibleProviderCount = await (from qualification in db.ProviderPrestationQualifications.AsNoTracking()
                                           join provider in db.Providers.AsNoTracking() on qualification.ProviderId equals provider.Id
                                           where provider.CompanyId == companyId
                                               && provider.Status == ProviderStatus.Approved
                                               && qualification.ServicePrestationId == servicePrestationId
                                               && qualification.Status == ProviderQualificationStatus.Approved
                                           select provider.Id).Distinct().CountAsync(cancellationToken);

        var summary = await db.CompanyQualitySummaries.FirstOrDefaultAsync(item =>
            item.CompanyId == companyId
            && item.ServiceId == serviceId
            && item.ServicePrestationId == servicePrestationId,
            cancellationToken);
        if (summary is null)
        {
            summary = new CompanyQualitySummary(companyId, serviceId, servicePrestationId);
            db.CompanyQualitySummaries.Add(summary);
        }

        summary.Recalculate(weightedScore, completedMissionCount, eligibleProviderCount, weightedRating, auditPassRate);
    }

    private async Task<int> CountCompletedMissionsAsync(
        Guid providerId,
        Guid serviceId,
        Guid? servicePrestationId,
        CancellationToken cancellationToken)
    {
        return await (from assignment in db.ProviderMissionAssignments.AsNoTracking()
                      join mission in db.Missions.AsNoTracking() on assignment.MissionId equals mission.Id
                      where assignment.ProviderId == providerId
                          && mission.ServiceId == serviceId
                          && mission.ServicePrestationId == servicePrestationId
                          && (mission.Status == MissionStatus.Completed || mission.Status == MissionStatus.Resolved)
                      select mission.Id).Distinct().CountAsync(cancellationToken);
    }

    private static bool ShouldAudit(Guid missionId, int completedCount)
    {
        if (completedCount <= 5) return true;
        var bucket = Math.Abs(missionId.GetHashCode()) % 100;
        return completedCount <= 20 ? bucket < 20 : bucket < 5;
    }

    private static ProviderQualityLevel ResolveLevel(int score, int completedCount, int failedAudits)
    {
        if (failedAudits > 0 && score < 60) return ProviderQualityLevel.UnderReview;
        if (completedCount < 5) return ProviderQualityLevel.New;
        if (completedCount < 20) return ProviderQualityLevel.Progressing;
        if (score >= 90) return ProviderQualityLevel.Excellence;
        if (score >= 80) return ProviderQualityLevel.Confirmed;
        return ProviderQualityLevel.Progressing;
    }
}
