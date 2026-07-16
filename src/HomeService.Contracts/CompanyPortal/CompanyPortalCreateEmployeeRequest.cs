namespace HomeService.Contracts.CompanyPortal;

public sealed record CompanyPortalCreateEmployeeRequest(
    string FirstName,
    string LastName,
    string PhoneNumber,
    string? Email,
    DateOnly DateOfBirth,
    string Address,
    string EmploymentType,
    int YearsOfExperience,
    decimal? MissionLatitude,
    decimal? MissionLongitude,
    int MissionRadiusKm,
    IReadOnlyList<Guid> ServiceIds,
    IReadOnlyList<CompanyPortalCreateEmployeeServiceRequest>? Services = null);

public sealed record CompanyPortalCreateEmployeeServiceRequest(
    Guid ServiceId,
    string ExperienceLevel,
    int YearsOfExperience,
    string PriceTier,
    IReadOnlyList<Guid> ServicePrestationIds);
