namespace HomeService.Contracts.Clients;

public sealed record StartClientMissionPaymentRequest(Guid PaymentMethodId);

public sealed record ClientMissionPaymentPreviewResponse(
    Guid MissionId,
    int ServiceAndPartsAmount,
    int CustomerServiceFeeAmount,
    int PaymentProviderFeeAmount,
    int TotalAmount,
    string Currency,
    int PaymentProviderFeeRateBasisPoints);

public sealed record ClientMissionPaymentResponse(
    Guid Id,
    Guid MissionId,
    string Reference,
    string Status,
    string ProviderCode,
    int ServiceAndPlatformAmount,
    int PaymentProviderFeeAmount,
    int TotalAmount,
    string Currency,
    string? RedirectUrl,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? CompletedAt,
    string? Message);
