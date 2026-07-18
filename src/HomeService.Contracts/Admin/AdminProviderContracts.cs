namespace HomeService.Contracts.Admin;

public sealed record AdminProviderListResponse(
    IReadOnlyList<AdminProviderSummaryResponse> Items,
    AdminProviderStatsResponse Stats);

public sealed record AdminProviderStatsResponse(
    int TotalProviders,
    int ApprovedProviders,
    int InterimCandidates,
    int SuspendedProviders,
    int AvailableProviders);

public sealed record AdminProviderSummaryResponse(
    Guid Id,
    Guid? CompanyId,
    string? CompanyName,
    string FullName,
    string PhoneNumber,
    string? Email,
    string Gender,
    string EmploymentType,
    string Status,
    bool IsAvailable,
    int YearsOfExperience,
    string Address,
    IReadOnlyList<string> Services,
    IReadOnlyList<string> Prestations,
    IReadOnlyList<AdminProviderDocumentSummaryResponse> Documents,
    DateTimeOffset CreatedAt);

public sealed record AdminProviderDocumentSummaryResponse(
    Guid Id,
    string DocumentType,
    string OriginalFileName,
    string ContentType,
    string PreviewUrl,
    DateTimeOffset CreatedAt);
