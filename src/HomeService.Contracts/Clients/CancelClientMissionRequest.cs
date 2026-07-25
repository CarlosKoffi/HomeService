namespace HomeService.Contracts.Clients;

public sealed record CancelClientMissionRequest(
    string CustomerPhoneNumber,
    string Reason,
    string? Comment);

public sealed record CancelClientMissionResponse(
    Guid MissionId,
    string MissionNumber,
    string Status,
    string PaymentStatus,
    int CancellationFeeAmount,
    int RefundAmount,
    string Currency,
    DateTimeOffset CancelledAt);
