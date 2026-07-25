namespace HomeService.Contracts.Clients;

public sealed record CreateClientMissionResponse(
    Guid MissionId,
    string MissionNumber,
    string Status,
    int CandidateCompanyCount,
    DateTimeOffset CreatedAt,
    string Message);
