namespace HomeService.Application.Missions;

public sealed record MissionDispatchScore(
    Guid CompanyId,
    string CompanyName,
    int Rank,
    int Score,
    string Details);
