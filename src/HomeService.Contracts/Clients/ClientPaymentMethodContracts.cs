namespace HomeService.Contracts.Clients;

public sealed record ClientPaymentMethodResponse(
    Guid Id,
    string Method,
    string Label,
    string? MaskedReference,
    bool IsDefault,
    bool IsActive);

public sealed record UpsertClientPaymentMethodRequest(
    string Method,
    string Label,
    string? MaskedReference,
    bool IsDefault);
