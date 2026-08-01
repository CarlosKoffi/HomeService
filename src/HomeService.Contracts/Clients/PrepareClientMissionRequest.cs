namespace HomeService.Contracts.Clients;

public sealed record PrepareClientMissionRequest(
    Guid ServiceId,
    Guid? ServicePrestationId,
    string Mode = "Instant",
    bool IsUrgent = false,
    Guid? ServiceOptionId = null);

public sealed record PrepareClientMissionResponse(
    Guid ServiceId,
    string ServiceName,
    Guid? ServicePrestationId,
    string? ServicePrestationName,
    string DisplayName,
    string? Description,
    string IconName,
    string? IconUrl,
    string? ImageUrl,
    int StartingPriceAmount,
    int MaximumPriceAmount,
    string Currency,
    bool RequiresCompanyQuote,
    bool PhotosRecommended,
    bool PhotosRequired,
    int MaxPhotoCount,
    int EstimatedDurationMinutes,
    string Mode,
    bool IsUrgent,
    bool UrgentOptionEnabled,
    int CompanyResponseMinutes,
    int CompanyAssignmentMinutes,
    IReadOnlyList<ClientMissionPaymentOptionResponse> PaymentOptions,
    string RecommendedPaymentMethod,
    string Message,
    Guid? ServiceOptionId = null,
    string? ServiceOptionName = null,
    bool IsFixedPrice = false,
    IReadOnlyList<ServiceOptionSummaryResponse>? AvailableOptions = null);

public sealed record ServiceOptionSummaryResponse(
    Guid Id,
    string Name,
    string? Description,
    int SortOrder,
    int PriceMinAmount,
    int PriceMaxAmount,
    bool IsFixedPrice,
    string Currency);

public sealed record ClientMissionPaymentOptionResponse(
    string Method,
    string Label,
    bool IsAvailable,
    bool IsRecommended);
