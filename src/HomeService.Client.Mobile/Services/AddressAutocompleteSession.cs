using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Services;

public sealed class AddressAutocompleteSession(ClientMobileApiClient apiClient) : IDisposable
{
    private readonly object sync = new();
    private CancellationTokenSource? activeSearch;
    private long searchVersion;
    private bool disposed;

    public string SessionToken { get; private set; } = Guid.NewGuid().ToString("N");

    public async Task<AddressSearchResult> SearchAsync(string? value)
    {
        var query = value?.Trim();
        if (string.IsNullOrWhiteSpace(query) || query.Length < 3)
        {
            CancelPendingSearch();
            return AddressSearchResult.Empty;
        }

        CancellationTokenSource cancellation;
        long version;
        lock (sync)
        {
            if (disposed)
            {
                return AddressSearchResult.Empty;
            }

            activeSearch?.Cancel();
            activeSearch = new CancellationTokenSource();
            cancellation = activeSearch;
            version = ++searchVersion;
        }

        try
        {
            await Task.Delay(400, cancellation.Token);
            var response = await apiClient.AutocompleteAddressAsync(query, SessionToken, cancellation.Token);
            if (cancellation.IsCancellationRequested || version != Volatile.Read(ref searchVersion))
            {
                return AddressSearchResult.Ignored;
            }

            return response.IsSuccess && response.Response is not null
                ? new AddressSearchResult(false, response.Response, null)
                : new AddressSearchResult(false, [], response.ErrorMessage ?? "Les suggestions sont momentanement indisponibles.");
        }
        catch (OperationCanceledException)
        {
            return AddressSearchResult.Ignored;
        }
        catch (Exception)
        {
            return new AddressSearchResult(false, [], "Les suggestions sont momentanement indisponibles.");
        }
    }

    public async Task<ClientPlaceDetailsResponse?> ResolveAsync(ClientAddressSuggestionResponse suggestion)
    {
        CancelPendingSearch();
        try
        {
            var response = await apiClient.GetPlaceDetailsAsync(suggestion.PlaceId, SessionToken);
            if (response.IsSuccess && response.Response is not null)
            {
                SessionToken = Guid.NewGuid().ToString("N");
                return response.Response;
            }
        }
        catch (Exception)
        {
            // The caller can still use the formatted suggestion without coordinates.
        }

        return null;
    }

    public void CancelPendingSearch()
    {
        lock (sync)
        {
            searchVersion++;
            activeSearch?.Cancel();
            activeSearch = null;
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            searchVersion++;
            activeSearch?.Cancel();
            activeSearch?.Dispose();
            activeSearch = null;
        }
    }
}

public sealed record AddressSearchResult(
    bool IsIgnored,
    IReadOnlyList<ClientAddressSuggestionResponse> Suggestions,
    string? ErrorMessage)
{
    public static AddressSearchResult Empty { get; } = new(false, [], null);
    public static AddressSearchResult Ignored { get; } = new(true, [], null);
}
