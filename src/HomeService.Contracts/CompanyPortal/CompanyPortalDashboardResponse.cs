namespace HomeService.Contracts.CompanyPortal;

public sealed record CompanyPortalDashboardResponse(
    Guid CompanyId,
    string CompanyName,
    string CompanyEmail,
    string CompanyStatus,
    string UserName,
    string UserEmail,
    int ProfileCompletionPercent,
    IReadOnlyList<CompanyPortalProgressStepResponse> ProgressSteps,
    int EmployeeCount,
    int AvailableEmployeeCount,
    int UpcomingMissionCount,
    int LiveMissionCount,
    int CompletedMissionCount,
    int MonthlyRevenueAmount,
    int CashToCollectAmount,
    string Currency,
    CompanyPortalMissionResponse? NextMission,
    IReadOnlyList<CompanyPortalActivityResponse> RecentActivities,
    IReadOnlyList<CompanyPortalEmployeeDigestResponse> EmployeeDigest);

public sealed record CompanyPortalProgressStepResponse(
    string Label,
    bool IsDone,
    string? ActionLabel,
    string? ActionUrl);

public sealed record CompanyPortalActivityResponse(
    Guid Id,
    string Type,
    string Title,
    string Description,
    string Tone,
    DateTimeOffset OccurredAt,
    bool IsRead);

public sealed record CompanyPortalEmployeeDigestResponse(
    Guid Id,
    string Initials,
    string FullName,
    string Role,
    string Status,
    bool IsAvailable,
    bool HasDiploma,
    string? PhotoUrl);

public sealed record CompanyPortalAssignableProviderResponse(
    Guid Id,
    string FullName,
    string PhoneNumber,
    string Status,
    bool IsAvailable,
    string EmploymentType,
    int YearsOfExperience,
    string ExperienceLevel,
    string PriceTier,
    int NormalPriceAmount,
    int PremiumPriceAmount,
    string Currency,
    bool HasDiploma,
    string? PhotoUrl,
    int? PriceMinAmount = null,
    int? PriceMaxAmount = null);

public sealed record AssignCompanyMissionRequest(
    Guid ProviderId,
    int QuotedAmount,
    string? OverMaxJustification);

public sealed record AssignCompanyMissionResponse(
    Guid MissionId,
    Guid ProviderId,
    Guid AssignmentId,
    string Status,
    DateTimeOffset ExpiresAt);
