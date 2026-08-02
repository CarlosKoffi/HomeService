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
    bool CanAccept,
    string? UnavailableReason);

public sealed record AdminProviderMissionTestListResponse(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<AdminProviderMissionTestAssignmentResponse> Assignments);

public sealed record AdminProviderMissionTestActionResponse(
    bool IsSuccess,
    string Message);
