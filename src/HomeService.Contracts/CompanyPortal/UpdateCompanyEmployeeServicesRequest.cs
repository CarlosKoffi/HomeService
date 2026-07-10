namespace HomeService.Contracts.CompanyPortal;

public sealed record UpdateCompanyEmployeeServicesRequest(
    IReadOnlyList<UpsertCompanyEmployeeServiceRequest> Services);

public sealed record UpsertCompanyEmployeeServiceRequest(
    Guid ServiceId,
    string ExperienceLevel,
    int YearsOfExperience,
    string PriceTier);
