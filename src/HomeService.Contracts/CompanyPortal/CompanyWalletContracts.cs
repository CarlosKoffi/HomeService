namespace HomeService.Contracts.CompanyPortal;

public sealed record CompanyWalletResponse(
    int PendingBalance,
    int AvailableBalance,
    int ReservedBalance,
    int WithdrawnBalance,
    string Currency,
    string SettlementFrequency,
    DateTimeOffset NextEligibilityDate,
    IReadOnlyList<CompanyPayoutDestinationResponse> Destinations,
    IReadOnlyList<CompanyPayoutResponse> Payouts,
    IReadOnlyList<CompanyWalletEntryResponse> Entries);

public sealed record CompanyPayoutDestinationResponse(
    Guid Id,
    string Method,
    string Label,
    string BeneficiaryName,
    string ProviderCode,
    string MaskedIdentifier,
    bool IsDefault,
    bool IsVerified,
    bool IsActive);

public sealed record CompanyPayoutResponse(
    Guid Id,
    string Reference,
    string Method,
    string Status,
    int GrossAmount,
    int FeeAmount,
    int NetAmount,
    string Currency,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    string? FailureReason);

public sealed record CompanyWalletEntryResponse(
    Guid Id,
    string Type,
    int Amount,
    string Currency,
    string Description,
    DateTimeOffset? EligibleAt,
    DateTimeOffset CreatedAt,
    Guid? MissionId,
    Guid? PayoutRequestId);

public sealed record UpdateCompanySettlementFrequencyRequest(string Frequency);

public sealed record CreateCompanyPayoutDestinationRequest(
    string Method,
    string Label,
    string BeneficiaryName,
    string ProviderCode,
    string Identifier,
    bool IsDefault);

public sealed record CreateCompanyPayoutRequest(Guid DestinationId, int? Amount = null);
