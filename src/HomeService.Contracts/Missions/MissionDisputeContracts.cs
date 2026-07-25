namespace HomeService.Contracts.Missions;

public sealed record MissionDisputeResponse(
    Guid Id,
    Guid MissionId,
    string Status,
    string OpenedBy,
    string Reason,
    string Description,
    string? Resolution,
    string? ResolutionNote,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ResolvedAt);

public sealed record ResolveMissionDisputeRequest(
    string Resolution,
    string Note);
