namespace HomeService.Contracts.Admin;

public sealed record AdminMissionSettingsResponse(
    IReadOnlyList<AdminCommissionRuleResponse> CommissionRules);

public sealed record AdminCommissionRuleResponse(
    Guid Id,
    string Name,
    string Target,
    string TargetLabel,
    string? ScopeLabel,
    int RateBasisPoints,
    decimal RatePercent,
    int FixedAmount,
    string Currency,
    bool IsActive,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveUntil);

public sealed record UpdateAdminCommissionRuleRequest(
    int RateBasisPoints,
    int FixedAmount,
    string Currency);
