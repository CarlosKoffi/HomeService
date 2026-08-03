namespace HomeService.Contracts.Admin;

public sealed record AdminProviderMissionTestAssignmentResponse(
    Guid AssignmentId,
    Guid MissionId,
    string MissionNumber,
    string ServiceLabel,
    string ProviderName,
    string CompanyName,
    string Address,
    DateTimeOffset ExpiresAt,
    string Status,
    bool CanAccept,
    bool CanStart,
    bool CanComplete,
    string? UnavailableReason);

public sealed record AdminProviderMissionTestListResponse(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<AdminProviderMissionTestAssignmentResponse> Assignments);

public sealed record AdminProviderMissionTestActionResponse(
    bool IsSuccess,
    string Message);

public sealed record AdminProviderMissionTestPositionRequest(int EstimatedArrivalMinutes);
