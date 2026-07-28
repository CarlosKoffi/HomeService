namespace HomeService.Contracts.Clients;

public sealed record CreateClientMissionResponse(
    Guid MissionId,
    string MissionNumber,
    string Status,
    int CandidateCompanyCount,
    int StartingPriceAmount,
    int MaximumPriceAmount,
    string Currency,
    DateTimeOffset CreatedAt,
    string Message);
