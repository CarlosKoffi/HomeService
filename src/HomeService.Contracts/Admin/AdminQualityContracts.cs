namespace HomeService.Contracts.Admin;

public sealed record AdminQualityDashboardResponse(
    AdminQualityStatsResponse Stats,
    IReadOnlyList<AdminQualityAuditResponse> PendingAudits,
    IReadOnlyList<AdminProviderQualityResponse> ProvidersAtRisk,
    IReadOnlyList<AdminCompanyQualityResponse> TopCompanies,
    IReadOnlyList<AdminQualityChecklistTemplateResponse> Templates);

public sealed record AdminQualityStatsResponse(
    int ActiveTemplates,
    int PendingAudits,
    int FailedAudits,
    int ApprovedQualifications,
    int PendingQualifications,
    int ProvidersUnderReview);

public sealed record AdminQualityChecklistTemplateResponse(
    Guid Id,
    Guid ServiceId,
    string ServiceName,
    Guid? ServicePrestationId,
    string? ServicePrestationName,
    string Name,
    string? Description,
    int Version,
    bool IsActive,
    IReadOnlyList<AdminQualityChecklistItemResponse> Items);

public sealed record AdminQualityChecklistItemResponse(
    Guid Id,
    string Code,
    string Label,
    string? Guidance,
    string Stage,
    string ResponseType,
    bool IsRequired,
    bool RequiresEvidenceOnIssue,
    Guid? ServiceOptionId,
    string? ServiceOptionName,
    int SortOrder,
    bool IsActive);

public sealed record CreateAdminQualityChecklistTemplateRequest(
    Guid ServiceId,
    Guid? ServicePrestationId,
    string Name,
    string? Description,
    bool CopyFromServiceDefault = true);

public sealed record UpdateAdminQualityChecklistTemplateRequest(
    string Name,
    string? Description,
    bool IsActive);

public sealed record CreateAdminQualityChecklistItemRequest(
    string Code,
    string Label,
    string? Guidance,
    string Stage,
    string ResponseType,
    bool IsRequired,
    bool RequiresEvidenceOnIssue,
    Guid? ServiceOptionId,
    int SortOrder);

public sealed record UpdateAdminQualityChecklistItemRequest(
    string Label,
    string? Guidance,
    string Stage,
    string ResponseType,
    bool IsRequired,
    bool RequiresEvidenceOnIssue,
    int SortOrder,
    bool IsActive);

public sealed record AdminProviderQualificationResponse(
    Guid Id,
    Guid ProviderId,
    string ProviderName,
    Guid ServiceId,
    string ServiceName,
    Guid ServicePrestationId,
    string ServicePrestationName,
    string Status,
    int? TheoryScore,
    int? PracticalScore,
    string? ReviewNote,
    DateTimeOffset? ReviewedAt,
    DateTimeOffset? ExpiresAt);

public sealed record ReviewAdminProviderQualificationRequest(
    string Status,
    int? TheoryScore,
    int? PracticalScore,
    string? ReviewNote,
    DateTimeOffset? ExpiresAt);

public sealed record AdminQualityAuditResponse(
    Guid Id,
    Guid MissionId,
    string MissionNumber,
    Guid ProviderId,
    string ProviderName,
    Guid CompanyId,
    string CompanyName,
    string ServiceName,
    string? ServicePrestationName,
    string Status,
    string SamplingReason,
    int? Score,
    string? ReviewNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt,
    int CompletedChecklistItems,
    int TotalChecklistItems,
    int EvidenceCount);

public sealed record ReviewAdminQualityAuditRequest(
    bool Passed,
    int Score,
    string? ReviewNote);

public sealed record AdminProviderQualityResponse(
    Guid ProviderId,
    string ProviderName,
    Guid ServiceId,
    string ServiceName,
    Guid? ServicePrestationId,
    string? ServicePrestationName,
    int Score,
    string Level,
    int CompletedMissionCount,
    int AuditedMissionCount,
    int PassedAuditCount,
    int ConfirmedIncidentCount,
    decimal AverageRating,
    decimal PunctualityRate,
    DateTimeOffset CalculatedAt);

public sealed record AdminCompanyQualityResponse(
    Guid CompanyId,
    string CompanyName,
    Guid ServiceId,
    string ServiceName,
    Guid? ServicePrestationId,
    string? ServicePrestationName,
    int Score,
    int CompletedMissionCount,
    int EligibleProviderCount,
    decimal AverageRating,
    decimal AuditPassRate,
    DateTimeOffset CalculatedAt);
