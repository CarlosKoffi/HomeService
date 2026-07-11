using System.Net.Http.Headers;
using System.Net.Http.Json;
using HomeService.Contracts.ProviderPortal;

namespace HomeService.Provider.Mobile.Services;

public sealed class ProviderMobileApiClient(HttpClient httpClient)
{
    public async Task<ProviderMobileHomeResponse?> GetHomeAsync(string bearerToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/provider-portal/mobile/home");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ProviderMobileHomeResponse>(cancellationToken);
    }
}
