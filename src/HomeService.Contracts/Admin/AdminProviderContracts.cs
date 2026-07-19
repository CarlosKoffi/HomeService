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

public sealed record AdminProviderDetailResponse(
    Guid Id,
    Guid? CompanyId,
    string? CompanyName,
    string FullName,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string? Email,
    DateOnly? DateOfBirth,
    string Gender,
    string EmploymentType,
    string Status,
    string RegistrationSource,
    bool IsAvailable,
    int YearsOfExperience,
    string Address,
    decimal? MissionLatitude,
    decimal? MissionLongitude,
    int MissionRadiusKm,
    decimal? CurrentLatitude,
    decimal? CurrentLongitude,
    DateTimeOffset CreatedAt,
    IReadOnlyList<AdminProviderServiceDetailResponse> Services,
    IReadOnlyList<AdminProviderServiceDetailResponse> CandidateServices,
    IReadOnlyList<AdminProviderDocumentSummaryResponse> Documents,
    IReadOnlyList<AdminProviderAffiliationRequestDetailResponse> AffiliationRequests,
    IReadOnlyList<AdminProviderMissionAssignmentDetailResponse> MissionAssignments);

public sealed record AdminProviderServiceDetailResponse(
    Guid ServiceId,
    string ServiceName,
    string ExperienceLevel,
    int YearsOfExperience,
    string PriceTier,
    bool IsActive,
    IReadOnlyList<string> Prestations);

public sealed record AdminProviderAffiliationRequestDetailResponse(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    string Status,
    string? Message,
    string? ReviewNote,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ReviewedAt);

public sealed record AdminProviderMissionAssignmentDetailResponse(
    Guid Id,
    Guid MissionId,
    string ServiceName,
    string CompanyName,
    string Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RespondedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? RefusalReason,
    string? RefusalComment,
    string ArrivalVerificationStatus,
    int? ArrivalDistanceMeters);

public sealed record AdminProviderActionRequest(string? Note);
