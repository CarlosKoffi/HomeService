namespace HomeService.Contracts.ProviderPortal;

public sealed record ProviderSelfRegistrationRequest(
    string FirstName,
    string LastName,
    string PhoneNumber,
    DateOnly DateOfBirth,
    string Address,
    string Gender,
    int YearsOfExperience,
    decimal? Latitude,
    decimal? Longitude,
    int MissionRadiusKm,
    string Password,
    string ConfirmPassword,
    IReadOnlyList<ProviderCandidateServiceRequest> Services,
    IReadOnlyList<string> ProposedServices,
    IReadOnlyList<ProviderCandidateSelectionRequest>? Selections = null,
    IReadOnlyList<Guid>? OpportunityCompanyIds = null);

public sealed record ProviderCandidateServiceRequest(
    Guid ServiceId,
    string ExperienceLevel,
    int YearsOfExperience);

public sealed record ProviderCandidateSelectionRequest(
    string Type,
    Guid Id,
    string ExperienceLevel,
    int YearsOfExperience);

public sealed record ProviderSelfRegistrationResponse(
    Guid ProviderId,
    string Status,
    string Message,
    IReadOnlyList<Guid>? AffiliationRequestIds = null);
