namespace HomeService.Contracts.Clients;

public sealed record ConfirmClientMissionRequest(
    string PhoneNumber,
    string? PaymentReference);

public sealed record ConfirmClientMissionResponse(
    Guid MissionId,
    string MissionNumber,
    string Status,
    string PaymentStatus,
    int TotalAmount,
    int PlatformCommissionAmount,
    int CompanyPayoutAmount,
    string Currency,
    bool ContactDetailsReleased,
    DateTimeOffset? ContactDetailsReleasedAt,
    string CompanyName,
    string CompanyPhoneNumber,
    string ProviderName,
    string ProviderPhoneNumber);
