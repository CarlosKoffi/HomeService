namespace HomeService.Contracts.CompanyPortal;

public sealed record CompanyPortalEmployeeResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string PhoneNumber,
    DateOnly? DateOfBirth,
    string Address,
    string EmploymentType,
    bool ReceivesDirectRequests,
    int YearsOfExperience,
    string Status,
    bool IsAvailable,
    decimal? MissionLatitude,
    decimal? MissionLongitude,
    int MissionRadiusKm,
    string? PhotoUrl,
    string? IdentityDocumentName,
    string? DiplomaDocumentName,
    bool HasDiploma,
    IReadOnlyList<CompanyPortalEmployeeServiceResponse> Services);
