namespace HomeService.Contracts.CompanyPortal;

public sealed record CompanyPortalProfileResponse(
    Guid CompanyId,
    Guid? ApplicationId,
    string CompanyName,
    string? RegistrationNumber,
    string City,
    string? Address,
    string ContactName,
    string Email,
    string PhoneNumber,
    string? PlannedServices,
    int? EstimatedProviderCount,
    string CompanyStatus,
    string ApplicationStatus,
    bool IsCompanyApproved,
    string? ReviewNote,
    IReadOnlyList<CompanyPortalProfileDocumentResponse> Documents);

public sealed record CompanyPortalProfileDocumentResponse(
    Guid Id,
    string DocumentType,
    string Label,
    string OriginalFileName,
    string ContentType,
    string ReviewStatus,
    string? ReviewNote,
    DateTimeOffset CreatedAt,
    string PreviewUrl);
