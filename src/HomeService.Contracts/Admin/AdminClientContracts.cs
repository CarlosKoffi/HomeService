namespace HomeService.Contracts.Admin;

public sealed record AdminClientListResponse(
    IReadOnlyList<AdminClientSummaryResponse> Items,
    AdminClientStatsResponse Stats);

public sealed record AdminClientStatsResponse(
    int TotalClients,
    int ClientsWithAddress,
    int ClientsWithPaymentMethod,
    int ClientsWithMissions);

public sealed record AdminClientSummaryResponse(
    Guid Id,
    string FullName,
    string PhoneNumber,
    string? Email,
    string? DefaultAddress,
    int AddressCount,
    int PaymentMethodCount,
    int MissionCount,
    int CompletedMissionCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record AdminClientDetailResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string PhoneNumber,
    string? Email,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<AdminClientAddressResponse> Addresses,
    IReadOnlyList<AdminClientPaymentMethodResponse> PaymentMethods,
    IReadOnlyList<AdminClientMissionResponse> Missions,
    IReadOnlyList<AdminClientAttachmentResponse> Attachments,
    IReadOnlyList<AdminClientNotificationResponse> Notifications,
    AdminClientActivitySummaryResponse Activity);

public sealed record AdminClientAddressResponse(
    Guid Id,
    string Label,
    string AddressLine,
    decimal? Latitude,
    decimal? Longitude,
    bool IsDefault);

public sealed record AdminClientPaymentMethodResponse(
    Guid Id,
    string Method,
    string Label,
    string? MaskedReference,
    bool IsDefault,
    bool IsActive);

public sealed record AdminClientMissionResponse(
    Guid Id,
    string MissionNumber,
    string ServiceName,
    string? PrestationName,
    string Status,
    string PaymentStatus,
    int Amount,
    string Currency,
    string? ServiceAddress,
    DateTimeOffset? ScheduledFor,
    DateTimeOffset CreatedAt);

public sealed record AdminClientAttachmentResponse(
    Guid Id,
    Guid MissionId,
    string MissionNumber,
    string AttachmentType,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    string? Caption,
    string PreviewUrl,
    DateTimeOffset CreatedAt);

public sealed record AdminClientNotificationResponse(
    Guid Id,
    string Channel,
    string Status,
    string Subject,
    string Body,
    DateTimeOffset ScheduledAt,
    DateTimeOffset? SentAt,
    DateTimeOffset? ReadAt);

public sealed record AdminClientActivitySummaryResponse(
    int TotalMissions,
    int CompletedMissions,
    int CancelledMissions,
    int DisputedMissions,
    int ReviewCount,
    decimal? AverageRating,
    int TotalPaidAmount,
    string Currency);
