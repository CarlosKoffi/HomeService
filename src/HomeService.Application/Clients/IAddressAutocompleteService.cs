using HomeService.Contracts.Clients;

namespace HomeService.Application.Clients;

public interface IAddressAutocompleteService
{
    Task<IReadOnlyList<ClientAddressSuggestionResponse>> SearchAsync(
        string query,
        string? sessionToken,
        CancellationToken cancellationToken);

    Task<ClientPlaceDetailsResponse?> GetDetailsAsync(
        string placeId,
        string? sessionToken,
        CancellationToken cancellationToken);
}
