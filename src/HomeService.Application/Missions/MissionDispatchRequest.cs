namespace HomeService.Application.Missions;

public sealed record MissionDispatchRequest(
    Guid MissionId,
    Guid ServiceId,
    Guid? ServicePrestationId,
    string? Zone,
    bool IsUrgent,
    int MaxCompanies = 3);
