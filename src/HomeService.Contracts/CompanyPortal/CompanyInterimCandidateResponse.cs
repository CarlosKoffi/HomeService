namespace HomeService.Contracts.CompanyPortal;

public sealed record CompanyInterimCandidateResponse(
    Guid RequestId,
    Guid ProviderId,
    string FirstName,
    string LastName,
    string PhoneNumber,
    DateOnly? BirthDate,
    string? Address,
    string Gender,
    int YearsOfExperience,
    string Status,
    string? Message,
    DateTimeOffset RequestedAt,
    IReadOnlyList<CompanyInterimCandidateServiceResponse> Services);

public sealed record CompanyInterimCandidateServiceResponse(
    Guid ServiceId,
    string ServiceName,
    string ExperienceLevel,
    int YearsOfExperience);

public sealed record CompanyReviewInterimCandidateRequest(string? Note);
