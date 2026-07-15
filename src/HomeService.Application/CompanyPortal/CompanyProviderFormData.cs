namespace HomeService.Application.CompanyPortal;

public sealed record CompanyProviderFormData(
    string FirstName,
    string LastName,
    string PhoneNumber,
    string? Email,
    DateOnly? DateOfBirth,
    string Address,
    int? YearsOfExperience,
    int? MissionRadiusKm,
    IReadOnlyList<Guid> ServiceIds,
    bool HasPhoto,
    bool HasIdentityDocument);
