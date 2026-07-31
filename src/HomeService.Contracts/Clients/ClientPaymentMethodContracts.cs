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

public sealed record SelectClientMissionPaymentMethodRequest(Guid PaymentMethodId);

public sealed record ClientMissionPaymentSelectionResponse(
    Guid MissionId,
    Guid PaymentMethodId,
    string Method,
    string Label,
    string? MaskedReference,
    bool IsReadyForQuoteConfirmation);
