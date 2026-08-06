using HomeService.Contracts.Clients;

namespace HomeService.Provider.Mobile.Services;

public sealed class ProviderAddressAutocompleteSession(ProviderMobileApiClient apiClient) : IDisposable
{
    private readonly object sync = new();
    private CancellationTokenSource? activeSearch;
    private long searchVersion;
    private bool disposed;

    public string SessionToken { get; private set; } = Guid.NewGuid().ToString("N");

    public async Task<ProviderAddressSearchResult> SearchAsync(string bearerToken, string? value)
    {
        var query = value?.Trim();
        if (string.IsNullOrWhiteSpace(query) || query.Length < 3)
        {
            CancelPendingSearch();
            return ProviderAddressSearchResult.Empty;
        }

        CancellationTokenSource cancellation;
        long version;
        lock (sync)
        {
            if (disposed)
            {
                return ProviderAddressSearchResult.Empty;
            }

            activeSearch?.Cancel();
            activeSearch?.Dispose();
            activeSearch = new CancellationTokenSource();
            cancellation = activeSearch;
            version = ++searchVersion;
        }

        try
        {
            await Task.Delay(400, cancellation.Token);
            var response = await apiClient.AutocompleteAddressAsync(
                bearerToken,
                query,
                SessionToken,
                cancellation.Token);
            if (cancellation.IsCancellationRequested || version != Volatile.Read(ref searchVersion))
            {
                return ProviderAddressSearchResult.Ignored;
            }

            return response.IsSuccess && response.Response is not null
                ? new ProviderAddressSearchResult(false, response.Response, null)
                : new ProviderAddressSearchResult(
                    false,
                    [],
                    response.ErrorMessage ?? "Les suggestions Google sont momentanément indisponibles.");
        }
        catch (OperationCanceledException)
        {
            return ProviderAddressSearchResult.Ignored;
        }
        catch (Exception)
        {
            return new ProviderAddressSearchResult(false, [], "Les suggestions Google sont momentanément indisponibles.");
        }
    }

    public async Task<ClientPlaceDetailsResponse?> ResolveAsync(
        string bearerToken,
        ClientAddressSuggestionResponse suggestion)
    {
        CancelPendingSearch();
        try
        {
            var response = await apiClient.GetPlaceDetailsAsync(
                bearerToken,
                suggestion.PlaceId,
                SessionToken);
            if (response.IsSuccess && response.Response is not null)
            {
                SessionToken = Guid.NewGuid().ToString("N");
                return response.Response;
            }
        }
        catch (Exception)
        {
            // The profile page displays a clear error and lets the provider retry.
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

public sealed record ProviderAddressSearchResult(
    bool IsIgnored,
    IReadOnlyList<ClientAddressSuggestionResponse> Suggestions,
    string? ErrorMessage)
{
    public static ProviderAddressSearchResult Empty { get; } = new(false, [], null);
    public static ProviderAddressSearchResult Ignored { get; } = new(true, [], null);
}
