namespace HomeService.Contracts.Admin;

public sealed record AdminDashboardResponse(
    int CompanyApplicationsToReview,
    int CompanyApplicationsWaitingActivation,
    int ActiveCompanies,
    int ProvidersToReview,
    int OpenMissions,
    int DisputedMissions,
    int PendingPaymentsAmount,
    int PlatformCommissionAmount,
    int UnreadCompanyPortalNotifications,
    int FailedExternalNotifications,
    IReadOnlyList<AdminDashboardActionResponse> PriorityActions);

public sealed record AdminDashboardActionResponse(
    string Label,
    string Description,
    string Url,
    string Tone,
    int Count);
