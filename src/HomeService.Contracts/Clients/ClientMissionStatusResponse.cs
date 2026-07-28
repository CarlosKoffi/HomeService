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
    IReadOnlyList<ClientMissionAdditionalQuoteResponse> AdditionalQuotes,
    IReadOnlyList<ClientMissionAttachmentResponse> Photos,
    ClientMissionAvailableActionsResponse Actions,
    string Message);

public sealed record ClientMissionAvailableActionsResponse(
    bool CanAcceptQuote,
    bool CanCancel,
    bool CanCallCompany,
    bool CanCallProvider,
    bool CanValidateCompletion,
    bool CanRateMission,
    bool CanOpenDispute,
    int? AmountToPayNow,
    string? PrimaryAction);

public sealed record ClientMissionCompanyResponse(
    Guid CompanyId,
    string Name,
    string? PhoneNumber,
    string? Email);

public sealed record ClientMissionProviderResponse(
    Guid ProviderId,
    string FullName,
    string? PhoneNumber,
    string? PhotoStoragePath,
    decimal? AverageRating,
    int CompletedMissionCount,
    int? EstimatedArrivalMinutes);

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

public sealed record ClientMissionAdditionalQuoteResponse(
    Guid QuoteId,
    string Status,
    string Reason,
    string? RequestedPhotoStoragePath,
    int? Amount,
    string Currency,
    string? CompanyDescription,
    DateTimeOffset RequestedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? PaidAt,
    bool CanPay);
