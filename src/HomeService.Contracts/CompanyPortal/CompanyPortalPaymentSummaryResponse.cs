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
    IReadOnlyList<CompanyPortalMissionResponse> Missions);
