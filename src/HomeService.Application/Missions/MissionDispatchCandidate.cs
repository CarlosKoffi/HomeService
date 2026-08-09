namespace HomeService.Application.Missions;

public sealed record MissionDispatchCandidate(
    Guid CompanyId,
    string CompanyName,
    int ManualPriority,
    bool CoversRequestedZone,
    bool AcceptsUrgentMissions,
    decimal? AverageRating,
    int CompletedMissionCount,
    int RecentMissionCount,
    int CancellationCount,
    int NoResponseCount,
    decimal? PriceDeviationPercent,
    int? QualityScore = null);
