namespace HomeService.Contracts.ProviderPortal;

public sealed record ProviderMissionQualityChecklistResponse(
    Guid? ControlId,
    string Status,
    int RequiredItemCount,
    int CompletedRequiredItemCount,
    bool CanStart,
    bool CanComplete,
    IReadOnlyList<ProviderMissionQualityStageResponse> Stages,
    int CompletionPercentage = 100,
    int MinimumCompletionPercentage = 50,
    bool CanCompleteWithException = true,
    string? CompletionRequirementMessage = null);

public sealed record ProviderMissionQualityStageResponse(
    string Stage,
    string Label,
    int RequiredItemCount,
    int CompletedRequiredItemCount,
    IReadOnlyList<ProviderMissionQualityItemResponse> Items);

public sealed record ProviderMissionQualityItemResponse(
    Guid ItemId,
    string Code,
    string Label,
    string? Guidance,
    string ResponseType,
    bool IsRequired,
    bool RequiresEvidenceOnIssue,
    int SortOrder,
    bool IsCompleted,
    bool? BooleanValue,
    decimal? NumberValue,
    string? TextValue,
    Guid? EvidenceAttachmentId,
    string? EvidencePhotoUrl = null,
    bool IsAvailable = true);

public sealed record UpdateProviderMissionQualityItemRequest(
    bool? BooleanValue,
    decimal? NumberValue,
    string? TextValue,
    Guid? EvidenceAttachmentId);

public sealed record ProviderMissionQualityPhotoUploadResponse(
    Guid AttachmentId,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes);
