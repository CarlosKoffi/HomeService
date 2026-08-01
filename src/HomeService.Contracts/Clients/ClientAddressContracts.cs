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

public sealed record ClientAddressSuggestionResponse(
    string PlaceId,
    string MainText,
    string SecondaryText,
    string FullText);

public sealed record ClientPlaceDetailsResponse(
    string PlaceId,
    string AddressLine,
    decimal Latitude,
    decimal Longitude);
