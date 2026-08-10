namespace HomeService.Contracts.Admin;

public sealed record AdminCompanyPayoutResponse(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    string Reference,
    string Method,
    string Status,
    string Destination,
    string BeneficiaryName,
    int GrossAmount,
    int FeeAmount,
    int NetAmount,
    string Currency,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    string? ProofReference,
    string? FailureReason);

public sealed record ReviewCompanyPayoutRequest(string? Reason = null, string? ProofReference = null);

public sealed record VerifyCompanyPayoutDestinationRequest(string? ExternalContactId = null);

public sealed record AdminCompanyPayoutDestinationResponse(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    string Method,
    string Label,
    string BeneficiaryName,
    string ProviderCode,
    string MaskedIdentifier,
    bool IsDefault,
    bool IsVerified,
    bool IsActive,
    DateTimeOffset CreatedAt);
