namespace HomeService.Contracts.Clients;

public sealed record ClientAddressResponse(
    Guid Id,
    string Label,
    string AddressLine,
    decimal? Latitude,
    decimal? Longitude,
    bool IsDefault);

public sealed record UpsertClientAddressRequest(
    string Label,
    string AddressLine,
    decimal? Latitude,
    decimal? Longitude,
    bool IsDefault);
