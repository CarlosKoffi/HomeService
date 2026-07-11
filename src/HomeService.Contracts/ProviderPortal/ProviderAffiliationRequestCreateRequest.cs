namespace HomeService.Contracts.ProviderPortal;

public sealed record ProviderAffiliationRequestCreateRequest(
    Guid ProviderId,
    Guid CompanyId,
    string? Message);

public sealed record ProviderAffiliationRequestResponse(
    Guid RequestId,
    Guid ProviderId,
    Guid CompanyId,
    string CompanyName,
    string Status,
    DateTimeOffset RequestedAt,
    string Message);
