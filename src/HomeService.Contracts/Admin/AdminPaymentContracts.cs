namespace HomeService.Contracts.Admin;

public sealed record AdminPaymentListResponse(
    IReadOnlyList<AdminPaymentMissionResponse> Items,
    AdminPaymentStatsResponse Stats);

public sealed record AdminPaymentStatsResponse(
    int TotalAmount,
    int PaidAmount,
    int PendingAmount,
    int CashToCollectAmount,
    int MobileMoneyAmount,
    int CardAmount,
    int PlatformCommissionAmount,
    int PendingPlatformCommissionAmount,
    int CompanyPayoutAmount,
    int RefundAmount,
    int DisputedAmount,
    int TransactionCount);

public sealed record AdminPaymentMissionResponse(
    Guid MissionId,
    string MissionNumber,
    string ServiceName,
    Guid? CompanyId,
    string? CompanyName,
    Guid? ProviderId,
    string CustomerName,
    string CustomerPhoneNumber,
    string? ProviderName,
    string? PrestationName,
    string MissionStatus,
    string PaymentStatus,
    string PaymentMethod,
    int Amount,
    int PlatformCommissionAmount,
    int PlatformCommissionRateBasisPoints,
    int CompanyPayoutAmount,
    int RefundAmount,
    int TransportFeeAmount,
    int CancellationFeeAmount,
    string Currency,
    DateTimeOffset? ScheduledFor,
    DateTimeOffset CreatedAt);
