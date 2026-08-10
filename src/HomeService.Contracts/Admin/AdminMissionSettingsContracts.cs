namespace HomeService.Contracts.Admin;

public sealed record AdminMissionSettingsResponse(
    IReadOnlyList<AdminCommissionRuleResponse> CommissionRules,
    IReadOnlyList<AdminMissionWorkflowSettingResponse> WorkflowSettings,
    IReadOnlyList<AdminCompanyCommissionTierResponse>? CompanyCommissionTiers = null);

public sealed record AdminCompanyCommissionTierResponse(
    Guid Id,
    string Name,
    int MinimumMissionCount,
    int RateBasisPoints,
    decimal RatePercent,
    int SortOrder,
    bool IsActive);

public sealed record UpdateAdminCompanyCommissionTierRequest(
    string Name,
    int MinimumMissionCount,
    int RateBasisPoints,
    int SortOrder,
    bool IsActive);

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

public sealed record AdminMissionWorkflowSettingResponse(
    Guid Id,
    string Key,
    string Label,
    string Description,
    string Unit,
    int Value,
    int MinimumValue,
    int MaximumValue,
    bool IsActive);

public sealed record UpdateAdminMissionWorkflowSettingRequest(int Value);
