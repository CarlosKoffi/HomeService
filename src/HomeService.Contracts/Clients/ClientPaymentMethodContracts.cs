namespace HomeService.Contracts.Clients;

public sealed record ClientPaymentMethodResponse(
    Guid Id,
    string Method,
    string Label,
    string? MaskedReference,
    bool IsDefault,
    bool IsActive,
    Guid? PaymentProviderId = null,
    string? PaymentProviderName = null,
    string? PaymentProviderLogoUrl = null);

public sealed record UpsertClientPaymentMethodRequest(
    string Method,
    string Label,
    string? MaskedReference,
    bool IsDefault,
    Guid? PaymentProviderId = null);

public sealed record CreateClientMobileMoneyAccountRequest(
    string PhoneNumber,
    IReadOnlyList<Guid> PaymentProviderIds,
    bool IsDefault);

public sealed record CreateClientMobileMoneyAccountResponse(
    string MaskedReference,
    IReadOnlyList<ClientPaymentMethodResponse> PaymentMethods);

public sealed record PaymentProviderResponse(
    Guid Id,
    string Code,
    string Name,
    string Method,
    string? Description,
    string? LogoUrl,
    bool IsActive,
    int SortOrder);

public sealed record UpsertPaymentProviderRequest(
    string Code,
    string Name,
    string Method,
    string? Description,
    string? LogoUrl,
    int SortOrder,
    bool IsActive);

public sealed record SelectClientMissionPaymentMethodRequest(Guid PaymentMethodId);

public sealed record ClientMissionPaymentSelectionResponse(
    Guid MissionId,
    Guid PaymentMethodId,
    string Method,
    string Label,
    string? MaskedReference,
    bool IsReadyForQuoteConfirmation);
