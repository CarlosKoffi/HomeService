namespace HomeService.Contracts.CompanyPortal;

public sealed record CompanyEmployeeServiceResponse(
    Guid ServiceId,
    string ServiceName,
    string ExperienceLevel,
    int YearsOfExperience,
    int HourlyRateAmount,
    string Currency,
    bool IsActive);
