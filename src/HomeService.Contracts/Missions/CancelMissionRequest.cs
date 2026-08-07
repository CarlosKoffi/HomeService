namespace HomeService.Contracts.Missions;

public sealed record CancelMissionRequest(
    string Reason,
    string? Comment,
    int? CancellationFeeAmount = null,
    int? RefundPercent = null,
    bool IncludeCustomerServiceFeeInRefund = false);

public sealed record CancelMissionResponse(
    Guid MissionId,
    string MissionNumber,
    string Status,
    string PaymentStatus,
    string CancelledBy,
    string CancellationReason,
    int CancellationFeeAmount,
    int RefundAmount,
    string Currency,
    DateTimeOffset CancelledAt);
