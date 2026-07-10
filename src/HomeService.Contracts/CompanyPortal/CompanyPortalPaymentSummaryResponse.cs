namespace HomeService.Contracts.CompanyPortal;

public sealed record CompanyPortalPaymentSummaryResponse(
    string Period,
    int TotalAmount,
    int MobileMoneyAmount,
    int CashAmount,
    int CashToCollectAmount,
    int MissionCount,
    string Currency,
    IReadOnlyList<CompanyPortalMissionResponse> Missions);
