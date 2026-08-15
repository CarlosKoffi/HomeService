namespace HomeService.Contracts.BusinessClients;

public sealed record UpsertBusinessClientProfileRequest(
    string LegalName,
    string? TradeName,
    string? LegalForm,
    string? RegistrationNumber,
    string? TaxIdentificationNumber,
    string Address,
    string City,
    string? CountryCode,
    string RepresentativeName,
    string RepresentativeRole,
    string ContactEmail,
    string ContactPhone);

public sealed record BusinessClientDocumentResponse(
    Guid Id,
    string DocumentType,
    string OriginalFileName,
    string ContentType,
    long Size,
    string ReviewStatus,
    string? ReviewNote,
    DateTimeOffset CreatedAt);

public sealed record BusinessClientProfileResponse(
    Guid Id,
    Guid CustomerId,
    string LegalName,
    string? TradeName,
    string? LegalForm,
    string? RegistrationNumber,
    string? TaxIdentificationNumber,
    string Address,
    string City,
    string CountryCode,
    string RepresentativeName,
    string RepresentativeRole,
    string ContactEmail,
    string ContactPhone,
    string Status,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ReviewedAt,
    string? ReviewNote,
    bool CanEdit,
    IReadOnlyList<BusinessClientDocumentResponse> Documents,
    IReadOnlyList<string> MissingRequiredDocuments);

public sealed record ReviewBusinessClientRequest(string? Note);

public sealed record AdminBusinessClientListItemResponse(
    Guid Id,
    Guid CustomerId,
    string LegalName,
    string? TradeName,
    string CustomerName,
    string ContactEmail,
    string ContactPhone,
    string Status,
    int DocumentCount,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminBusinessClientDetailResponse(
    BusinessClientProfileResponse Profile,
    string CustomerName,
    string CustomerPhone,
    string? CustomerEmail);
