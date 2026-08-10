namespace HomeService.Contracts.CompanyPortal;

public sealed record CompanyPortalPaymentSummaryResponse(
    string Period,
    int TotalAmount,
    int MobileMoneyAmount,
    int CardAmount,
    int CashAmount,
    int CashToCollectAmount,
    int PlatformRevenueAmount,
    int MissionCount,
    string Currency,
    IReadOnlyList<CompanyPortalMissionResponse> Missions,
    int GrossServiceAmount = 0,
    int CompanyCommissionAmount = 0,
    int CompanyNetAmount = 0,
    IReadOnlyList<CompanyPortalPaymentBreakdownResponse>? FinancialBreakdowns = null,
    CompanyPortalCommissionProgressResponse? CommissionProgress = null);

public sealed record CompanyPortalCommissionProgressResponse(
    string CurrentTierName,
    int CurrentRateBasisPoints,
    int CompletedMissionCount,
    int? NextTierMinimumMissionCount,
    string? NextTierName,
    int MissionsUntilNextTier,
    int RatingCount,
    decimal AverageRating,
    int CompanyCancellationRateBasisPoints,
    bool DocumentsCompliant,
    bool HasOpenDispute,
    bool IsQualityEligible);

public sealed record CompanyPortalPaymentBreakdownResponse(
    Guid MissionId,
    int GrossServiceAmount,
    int CommissionRateBasisPoints,
    int CommissionAmount,
    int CompanyNetAmount,
    int CommissionableAmount,
    bool IsFirstCustomerCompanyOrder,
    int PartsEstimateAmount,
    string? CompanyCommissionTierName = null,
    int CompanyCommissionMissionSequence = 0);
