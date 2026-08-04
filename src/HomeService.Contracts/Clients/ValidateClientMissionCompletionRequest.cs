namespace HomeService.Contracts.Clients;

public sealed record ValidateClientMissionCompletionRequest(
    string PhoneNumber,
    int QualityRating,
    int PunctualityRating,
    int PresentationRating,
    int PolitenessRating,
    int CleanlinessRating,
    string? Comment,
    string? PayoutReference,
    IReadOnlyList<ClientMissionPhotoRequest>? Photos = null);

public sealed record ValidateClientMissionCompletionResponse(
    Guid MissionId,
    string MissionNumber,
    string Status,
    string PaymentStatus,
    DateTimeOffset? CustomerCompletionValidatedAt,
    DateTimeOffset? CompanyPayoutReleasedAt,
    int OverallRating,
    int CompanyPayoutAmount,
    string Currency);
