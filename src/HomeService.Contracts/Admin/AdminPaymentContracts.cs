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
    int PlatformCommissionAmount,
    int DisputedAmount,
    int TransactionCount);

public sealed record AdminPaymentMissionResponse(
    Guid MissionId,
    string ServiceName,
    string? CompanyName,
    string CustomerName,
    string CustomerPhoneNumber,
    string? ProviderName,
    string MissionStatus,
    string PaymentStatus,
    string PaymentMethod,
    int Amount,
    int PlatformCommissionAmount,
    int TransportFeeAmount,
    int CancellationFeeAmount,
    string Currency,
    DateTimeOffset? ScheduledFor,
    DateTimeOffset CreatedAt);
