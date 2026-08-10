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
    IReadOnlyList<CompanyPortalPaymentBreakdownResponse>? FinancialBreakdowns = null);

public sealed record CompanyPortalPaymentBreakdownResponse(
    Guid MissionId,
    int GrossServiceAmount,
    int CommissionRateBasisPoints,
    int CommissionAmount,
    int CompanyNetAmount,
    int CommissionableAmount,
    bool IsFirstCustomerCompanyOrder,
    int PartsEstimateAmount);
