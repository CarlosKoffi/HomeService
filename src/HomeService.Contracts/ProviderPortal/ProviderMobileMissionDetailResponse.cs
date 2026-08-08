namespace HomeService.Contracts.ProviderPortal;

public sealed record ProviderMobileMissionDetailResponse(
    Guid AssignmentId,
    Guid MissionId,
    string MissionNumber,
    string AssignmentStatus,
    string MissionStatus,
    string ServiceName,
    string ServiceIconName,
    string? PrestationName,
    string CompanyName,
    string CustomerDisplayName,
    string? CustomerPhoneNumber,
    bool CanCallCustomer,
    string LocationLabel,
    decimal? Latitude,
    decimal? Longitude,
    double? DistanceKm,
    DateTimeOffset? ScheduledFor,
    DateTimeOffset ExpiresAt,
    int SecondsToRespond,
    string? PartsDescription,
    string? Description,
    ProviderMobileMissionActionsResponse Actions,
    ProviderMobileMissionArrivalResponse Arrival,
    IReadOnlyList<ProviderMobileMissionAdditionalQuoteResponse> AdditionalQuotes,
    IReadOnlyList<ProviderMobileMissionPhotoResponse> CustomerPhotos,
    IReadOnlyList<ProviderMobileMissionMessageResponse> RecentMessages);

public sealed record ProviderMobileMissionActionsResponse(
    bool CanAccept,
    bool CanRefuse,
    bool CanMarkOnTheWay,
    DateTimeOffset? MarkOnTheWayAutomaticallyAt,
    bool CanVerifyArrival,
    bool CanStart,
    bool CanComplete,
    bool CanCancel);

public sealed record ProviderMobileMissionArrivalResponse(
    string Status,
    bool IsVerified,
    int? DistanceMeters,
    int ToleranceMeters,
    int? AccuracyMeters,
    DateTimeOffset? VerifiedAt);

public sealed record ProviderMobileMissionPhotoResponse(
    Guid AttachmentId,
    string OriginalFileName,
    string StoragePath,
    string ContentType,
    string? Caption);

public sealed record ProviderMobileMissionAdditionalQuoteResponse(
    Guid QuoteId,
    string Status,
    string Reason,
    string? RequestedPhotoStoragePath,
    string? CompanyDescription,
    DateTimeOffset RequestedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? PaidAt);

public sealed record ProviderMobileMissionMessageResponse(
    Guid MessageId,
    string SenderType,
    string Body,
    string? AttachmentPath,
    string? AttachmentContentType,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);
