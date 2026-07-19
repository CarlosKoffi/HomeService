namespace HomeService.Contracts.Admin;

public sealed record AdminCompanyListResponse(
    IReadOnlyList<AdminCompanySummaryResponse> Items,
    AdminCompanyStatsResponse Stats);

public sealed record AdminCompanyStatsResponse(
    int TotalCompanies,
    int ApprovedCompanies,
    int SuspendedCompanies,
    int TotalProviders,
    int ActiveProviders,
    int OpenMissions,
    int DisputedMissions);

public sealed record AdminCompanySummaryResponse(
    Guid Id,
    string Name,
    string? Email,
    string PhoneNumber,
    string? City,
    string Status,
    string AssignmentMode,
    int ProviderCount,
    int ActiveProviderCount,
    int MissionCount,
    int OpenMissionCount,
    int DocumentCount,
    DateTimeOffset CreatedAt);

public sealed record AdminCompanyActionRequest(string? Note);

public sealed record AdminCompanyDetailResponse(
    Guid Id,
    string Name,
    string? Email,
    string PhoneNumber,
    string? LegalForm,
    string? RegistrationNumber,
    string? TaxIdentificationNumber,
    string? City,
    string? Address,
    string? InterventionZones,
    string? PlannedServices,
    string? WavePaymentNumber,
    string? OrangeMoneyPaymentNumber,
    string Status,
    string AssignmentMode,
    DateTimeOffset CreatedAt,
    IReadOnlyList<AdminCompanyProviderResponse> Providers,
    IReadOnlyList<AdminCompanyMissionResponse> Missions,
    IReadOnlyList<AdminCompanyDocumentResponse> Documents,
    IReadOnlyList<AdminCompanyApplicationTimelineResponse> Timeline)
{
    public AdminCompanyOperationsSummaryResponse Summary { get; init; } = AdminCompanyOperationsSummaryResponse.Empty;
    public IReadOnlyList<AdminCompanyInterimRequestResponse> InterimRequests { get; init; } = [];
    public IReadOnlyList<AdminCompanyApplicationDocumentResponse> ApplicationDocuments { get; init; } = [];
    public IReadOnlyList<AdminCompanyNotificationResponse> Notifications { get; init; } = [];
}

public sealed record AdminCompanyOperationsSummaryResponse(
    int ProviderCount,
    int ActiveProviderCount,
    int InterimProviderCount,
    int PendingInterimRequestCount,
    int OpenMissionCount,
    int CompletedMissionCount,
    int DisputedMissionCount,
    int CompanyDocumentCount,
    int ValidCompanyDocumentCount,
    int ProviderDocumentCount,
    int UnreadNotificationCount,
    int PendingPaymentAmount,
    string Currency)
{
    public static AdminCompanyOperationsSummaryResponse Empty { get; } = new(
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        "XOF");
}

public sealed record AdminCompanyProviderResponse(
    Guid Id,
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
    int DocumentCount,
    DateTimeOffset CreatedAt);

public sealed record AdminCompanyMissionResponse(
    Guid Id,
    string ServiceName,
    string? PrestationName,
    string CustomerName,
    string CustomerPhoneNumber,
    string? ProviderName,
    string Status,
    string PaymentStatus,
    string PaymentMethod,
    DateTimeOffset? ScheduledFor,
    int? EstimatedTotalAmount,
    int? CompanyQuotedAmount,
    string Currency,
    string? ServiceAddress,
    DateTimeOffset CreatedAt);

public sealed record AdminCompanyInterimRequestResponse(
    Guid Id,
    Guid ProviderId,
    string ProviderName,
    string PhoneNumber,
    string? Email,
    string Gender,
    string Status,
    string? Message,
    string? ReviewNote,
    IReadOnlyList<string> Services,
    IReadOnlyList<string> Prestations,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ReviewedAt);

public sealed record AdminCompanyApplicationDocumentResponse(
    Guid Id,
    string DocumentType,
    string OriginalFileName,
    string ContentType,
    string ReviewStatus,
    string? ReviewNote,
    string PreviewUrl,
    DateTimeOffset CreatedAt);

public sealed record AdminCompanyDocumentResponse(
    Guid Id,
    Guid ProviderId,
    string ProviderName,
    string DocumentType,
    string OriginalFileName,
    string ContentType,
    string PreviewUrl,
    DateTimeOffset CreatedAt);

public sealed record AdminCompanyNotificationResponse(
    Guid Id,
    string Type,
    string Title,
    string Message,
    string Tone,
    bool IsRead,
    string? ActionUrl,
    DateTimeOffset OccurredAt);

public sealed record AdminCompanyApplicationTimelineResponse(
    Guid Id,
    string? PreviousStatus,
    string NewStatus,
    string? Note,
    string? ChangedBy,
    DateTimeOffset ChangedAt);
