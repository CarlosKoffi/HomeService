namespace HomeService.Contracts.ProviderPortal;

public sealed record ProviderLocationVerificationResponse(
    Guid AssignmentId,
    Guid MissionId,
    string Status,
    bool IsVerified,
    int? DistanceMeters,
    int ToleranceMeters,
    int? AccuracyMeters,
    string Message,
    DateTimeOffset? VerifiedAt);
