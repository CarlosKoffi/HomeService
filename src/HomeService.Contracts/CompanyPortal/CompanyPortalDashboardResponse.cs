namespace HomeService.Contracts.CompanyPortal;

public sealed record CompanyPortalDashboardResponse(
    string CompanyName,
    int EmployeeCount,
    int AvailableEmployeeCount,
    int UpcomingMissionCount,
    int LiveMissionCount,
    int CompletedMissionCount,
    int MonthlyRevenueAmount,
    int CashToCollectAmount,
    string Currency);
