namespace HomeService.Contracts.CompanyPortal;

public sealed record CompanyPortalEmployeeServiceResponse(
    Guid ServiceId,
    string ServiceName,
    string ExperienceLevel,
    int YearsOfExperience,
    int HourlyRateAmount,
    string Currency,
    bool IsActive);
