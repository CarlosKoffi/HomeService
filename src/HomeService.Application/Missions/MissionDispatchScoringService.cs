namespace HomeService.Application.Missions;

public sealed class MissionDispatchScoringService
{
    private const int ManualPriorityWeight = 1000;
    private const int ZonePenalty = 300;
    private const int UrgentCapabilityBonus = 180;
    private const int UrgentMissingCapabilityPenalty = 350;
    private const int MaxReputationBonus = 220;
    private const int RecentMissionPenalty = 35;
    private const int CancellationPenalty = 120;
    private const int NoResponsePenalty = 80;
    private const int PriceDeviationPenaltyWeight = 6;
    private const int CompletedMissionExperienceBonusCap = 120;
    private const int MaxQualityBonus = 320;

    public IReadOnlyList<MissionDispatchScore> SelectTopCompanies(
        MissionDispatchRequest request,
        IEnumerable<MissionDispatchCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidates);

        var maxCompanies = Math.Clamp(request.MaxCompanies, 1, 10);

        return candidates
            .GroupBy(candidate => candidate.CompanyId)
            .Select(group => Score(request, group.First()))
            .OrderBy(score => score.Score)
            .ThenBy(score => score.CompanyName, StringComparer.OrdinalIgnoreCase)
            .Take(maxCompanies)
            .Select((score, index) => score with { Rank = index + 1 })
            .ToList();
    }

    public MissionDispatchScore Score(MissionDispatchRequest request, MissionDispatchCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidate);

        var priority = Math.Clamp(candidate.ManualPriority, 0, 9999);
        var score = priority * ManualPriorityWeight;

        var reputationBonus = CalculateReputationBonus(candidate.AverageRating);
        var experienceBonus = Math.Min(candidate.CompletedMissionCount * 8, CompletedMissionExperienceBonusCap);
        var recentPenalty = Math.Max(0, candidate.RecentMissionCount) * RecentMissionPenalty;
        var cancellationPenalty = Math.Max(0, candidate.CancellationCount) * CancellationPenalty;
        var noResponsePenalty = Math.Max(0, candidate.NoResponseCount) * NoResponsePenalty;
        var pricePenalty = CalculatePricePenalty(candidate.PriceDeviationPercent);
        var zonePenalty = candidate.CoversRequestedZone ? 0 : ZonePenalty;
        var urgencyAdjustment = CalculateUrgencyAdjustment(request.IsUrgent, candidate.AcceptsUrgentMissions);
        var qualityBonus = candidate.QualityScore is null ? 0 : (int)Math.Round(Math.Clamp(candidate.QualityScore.Value, 0, 100) / 100d * MaxQualityBonus);

        score -= reputationBonus;
        score -= experienceBonus;
        score += recentPenalty;
        score += cancellationPenalty;
        score += noResponsePenalty;
        score += pricePenalty;
        score += zonePenalty;
        score += urgencyAdjustment;
        score -= qualityBonus;

        var details = string.Join("; ", new[]
        {
            $"priority={priority}",
            $"reputationBonus={reputationBonus}",
            $"experienceBonus={experienceBonus}",
            $"recentPenalty={recentPenalty}",
            $"cancellationPenalty={cancellationPenalty}",
            $"noResponsePenalty={noResponsePenalty}",
            $"pricePenalty={pricePenalty}",
            $"zonePenalty={zonePenalty}",
            $"urgencyAdjustment={urgencyAdjustment}",
            $"qualityBonus={qualityBonus}"
        });

        return new MissionDispatchScore(candidate.CompanyId, candidate.CompanyName, Rank: 0, Math.Max(0, score), details);
    }

    private static int CalculateReputationBonus(decimal? averageRating)
    {
        if (averageRating is null)
        {
            return 0;
        }

        var normalized = Math.Clamp((double)averageRating.Value, 0, 5) / 5d;
        return (int)Math.Round(normalized * MaxReputationBonus);
    }

    private static int CalculatePricePenalty(decimal? priceDeviationPercent)
    {
        if (priceDeviationPercent is null)
        {
            return 0;
        }

        var normalizedDeviation = Math.Max(0, Math.Abs((double)priceDeviationPercent.Value));
        return (int)Math.Round(normalizedDeviation * PriceDeviationPenaltyWeight);
    }

    private static int CalculateUrgencyAdjustment(bool isUrgent, bool acceptsUrgentMissions)
    {
        if (!isUrgent)
        {
            return 0;
        }

        return acceptsUrgentMissions ? -UrgentCapabilityBonus : UrgentMissingCapabilityPenalty;
    }
}
