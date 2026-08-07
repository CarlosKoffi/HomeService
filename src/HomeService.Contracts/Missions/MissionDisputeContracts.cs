namespace HomeService.Contracts.Missions;

public sealed record MissionDisputeResponse(
    Guid Id,
    Guid MissionId,
    string Status,
    string OpenedBy,
    string Reason,
    string Description,
    string? Resolution,
    int? RefundPercent,
    int? RefundAmount,
    string Currency,
    string? ResolutionNote,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ResolvedAt);

public sealed record OpenMissionDisputeRequest(
    string Reason,
    string Description);

public sealed record ResolveMissionDisputeRequest(
    string Resolution,
    string Note,
    int? RefundPercent = null,
    int? RefundAmount = null,
    bool IncludeCustomerServiceFeeInRefund = false);
