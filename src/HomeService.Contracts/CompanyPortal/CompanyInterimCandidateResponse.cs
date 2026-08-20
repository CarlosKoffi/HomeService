namespace HomeService.Contracts.CompanyPortal;

public sealed record CompanyInterimSettingsResponse(
    Guid CompanyId,
    bool AcceptsInterimApplications,
    string Message);

public sealed record UpdateCompanyInterimSettingsRequest(bool AcceptsInterimApplications);

public sealed record CompanyInterimCandidateResponse(
    Guid RequestId,
    Guid ProviderId,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string? Email,
    DateOnly? BirthDate,
    string? Address,
    string Gender,
    int YearsOfExperience,
    string Status,
    string? Message,
    string TargetCompanyName,
    string? ReviewNote,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ReviewedAt,
    string? PhotoUrl,
    IReadOnlyList<CompanyInterimCandidateServiceResponse> Services,
    IReadOnlyList<CompanyEmployeeDocumentResponse> Documents,
    IReadOnlyList<CompanyInterimCandidateAffiliationResponse> Applications,
    bool CandidateMetAndTestedByCompany = false,
    bool CompetencyValidatedByCompany = false,
    bool SeriousnessValidatedByCompany = false,
    bool PunctualityValidatedByCompany = false,
    DateTimeOffset? CompanyValidationAttestedAt = null);

public sealed record CompanyInterimCandidateServiceResponse(
    Guid ServiceId,
    string ServiceName,
    string ExperienceLevel,
    int YearsOfExperience);

public sealed record CompanyInterimCandidateAffiliationResponse(
    Guid RequestId,
    Guid CompanyId,
    string CompanyName,
    string Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ReviewedAt,
    bool IsCurrentCompany);

public sealed record CompanyReviewInterimCandidateRequest(
    string? Note,
    bool CandidateMetAndTestedByCompany,
    bool CompetencyValidatedByCompany,
    bool SeriousnessValidatedByCompany,
    bool PunctualityValidatedByCompany);
