namespace HomeService.Contracts.CompanyPortal;

public sealed record UpdateCompanyEmployeeRequest(
    string FirstName,
    string LastName,
    string PhoneNumber,
    string? Email,
    DateOnly DateOfBirth,
    string Address,
    string Gender,
    string EmploymentType,
    int YearsOfExperience,
    decimal? MissionLatitude,
    decimal? MissionLongitude,
    int MissionRadiusKm);
