namespace HomeService.Contracts.CompanyPortal;

public sealed record UpdateCompanyEmployeeRequest(
    string FirstName,
    string LastName,
    string PhoneNumber,
    DateOnly DateOfBirth,
    string Address,
    string Gender,
    string EmploymentType,
    int YearsOfExperience,
    int MissionRadiusKm);
