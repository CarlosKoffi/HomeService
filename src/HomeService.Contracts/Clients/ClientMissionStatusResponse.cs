namespace HomeService.Contracts.Clients;

public sealed record ClientMissionStatusResponse(
    Guid MissionId,
    string MissionNumber,
    string Status,
    string QuoteStatus,
    string PaymentStatus,
    string Mode,
    string PaymentMethod,
    string? ServiceName,
    string? PrestationName,
    string? Description,
    string? ServiceAddress,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ScheduledFor,
    DateTimeOffset? CompanyQuotedAt,
    DateTimeOffset? ProviderAcceptedAt,
    DateTimeOffset? CustomerConfirmedAt,
    DateTimeOffset? CustomerCompletionValidatedAt,
    int? EstimatedTotalAmount,
    int? CompanyQuotedAmount,
    int? PartsEstimateAmount,
    string? PartsDescription,
    int? FinalTotalAmount,
    int PlatformCommissionAmount,
    int CompanyPayoutAmount,
    int TransportFeeAmount,
    string Currency,
    bool RequiresCompanyQuote,
    bool ContactDetailsReleased,
    ClientMissionCompanyResponse? AssignedCompany,
    ClientMissionProviderResponse? AssignedProvider,
    IReadOnlyList<ClientMissionOfferResponse> CompanyOffers,
    IReadOnlyList<ClientMissionAttachmentResponse> Photos,
    string Message);

public sealed record ClientMissionCompanyResponse(
    Guid CompanyId,
    string Name,
    string? PhoneNumber,
    string? Email);

public sealed record ClientMissionProviderResponse(
    Guid ProviderId,
    string FullName,
    string? PhoneNumber,
    string? PhotoStoragePath);

public sealed record ClientMissionOfferResponse(
    Guid OfferId,
    Guid CompanyId,
    string CompanyName,
    int Rank,
    int Score,
    string Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RespondedAt);

public sealed record ClientMissionAttachmentResponse(
    Guid AttachmentId,
    string OriginalFileName,
    string StoragePath,
    string ContentType,
    long FileSizeBytes,
    string? Caption);
