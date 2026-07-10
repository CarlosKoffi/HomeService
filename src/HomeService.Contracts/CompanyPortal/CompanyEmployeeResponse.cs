namespace HomeService.Contracts.CompanyPortal;

public sealed record CompanyEmployeeResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string PhoneNumber,
    DateOnly? BirthDate,
    string? Address,
    string EmploymentType,
    bool ReceivesDirectRequests,
    int YearsOfExperience,
    string Status,
    bool IsAvailable,
    decimal? CurrentLatitude,
    decimal? CurrentLongitude,
    decimal? ServiceRadiusKm,
    string? PhotoUrl,
    string? IdentityDocumentUrl,
    string? DiplomaDocumentUrl,
    bool HasDiploma,
    IReadOnlyList<CompanyEmployeeServiceResponse> Services,
    DateTimeOffset CreatedAt);
