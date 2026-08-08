using System.Net.Http.Json;
using HomeService.Contracts.Clients;
using HomeService.Contracts.Services;

namespace HomeService.Client.Services;

public sealed class PublicWebsiteApiClient(HttpClient httpClient, ILogger<PublicWebsiteApiClient> logger)
{
    public async Task<IReadOnlyList<ServiceSummaryResponse>> GetServicesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<IReadOnlyList<ServiceSummaryResponse>>(
                       "api/services",
                       cancellationToken)
                   ?? [];
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "The public service catalog is temporarily unavailable.");
            return [];
        }
    }

    public async Task<PublicWebsiteApiResult<PublicServiceAvailabilityResponse>> CheckAvailabilityAsync(
        PublicServiceAvailabilityRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                "api/public/services/availability",
                request,
                cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<PublicServiceAvailabilityResponse>(
                    cancellationToken: cancellationToken);
                return result is null
                    ? PublicWebsiteApiResult<PublicServiceAvailabilityResponse>.Failed("La réponse de disponibilité est incomplète.")
                    : PublicWebsiteApiResult<PublicServiceAvailabilityResponse>.Ok(result);
            }

            var error = await response.Content.ReadFromJsonAsync<PublicWebsiteApiError>(
                cancellationToken: cancellationToken);
            return PublicWebsiteApiResult<PublicServiceAvailabilityResponse>.Failed(
                error?.Message ?? "La disponibilité n’a pas pu être vérifiée.");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "The public availability preview is temporarily unavailable.");
            return PublicWebsiteApiResult<PublicServiceAvailabilityResponse>.Failed(
                "La vérification en direct est momentanément indisponible. Vous pouvez continuer dans l’application Wélé.");
        }
    }

    public async Task<IReadOnlyList<ClientAddressSuggestionResponse>> SearchAddressesAsync(
        string query,
        string sessionToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<IReadOnlyList<ClientAddressSuggestionResponse>>(
                       $"api/public/addresses/autocomplete?query={Uri.EscapeDataString(query)}&sessionToken={Uri.EscapeDataString(sessionToken)}",
                       cancellationToken)
                   ?? [];
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "The public Google Places autocomplete is temporarily unavailable.");
            return [];
        }
    }

    public async Task<ClientPlaceDetailsResponse?> GetPlaceDetailsAsync(
        string placeId,
        string sessionToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<ClientPlaceDetailsResponse>(
                $"api/public/addresses/places/{Uri.EscapeDataString(placeId)}?sessionToken={Uri.EscapeDataString(sessionToken)}",
                cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "The public Google Place details request is temporarily unavailable.");
            return null;
        }
    }
}

public sealed record PublicWebsiteApiResult<T>(bool IsSuccess, string? ErrorMessage, T? Value)
{
    public static PublicWebsiteApiResult<T> Ok(T value) => new(true, null, value);
    public static PublicWebsiteApiResult<T> Failed(string message) => new(false, message, default);
}

public sealed record PublicWebsiteApiError(string? Message);
