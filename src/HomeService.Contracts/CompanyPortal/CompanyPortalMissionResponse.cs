namespace HomeService.Contracts.CompanyPortal;

public sealed record CompanyPortalMissionResponse(
    Guid Id,
    string ServiceName,
    string CustomerName,
    string CustomerPhoneNumber,
    string Mode,
    string Status,
    string PaymentMethod,
    string PaymentStatus,
    DateTimeOffset? ScheduledFor,
    int EstimatedDurationMinutes,
    int? FinalTotalAmount,
    string Currency,
    Guid? ProviderId,
    string? ProviderName,
    int? CompanyQuotedAmount = null,
    string? CompanyQuoteJustification = null,
    DateTimeOffset? CompanyQuotedAt = null,
    DateTimeOffset? CustomerQuoteAcceptedAt = null);
