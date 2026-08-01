using System.Net.Http.Json;
using System.Text.Json;
using HomeService.Application.Clients;
using HomeService.Contracts.Clients;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HomeService.Infrastructure.Location;

public sealed class GooglePlacesAddressAutocompleteService(
    HttpClient httpClient,
    IOptions<GooglePlacesOptions> options,
    ILogger<GooglePlacesAddressAutocompleteService> logger) : IAddressAutocompleteService
{
    private const string PlacesBaseUrl = "https://places.googleapis.com/v1/";
    private readonly GooglePlacesOptions settings = options.Value;

    public async Task<IReadOnlyList<ClientAddressSuggestionResponse>> SearchAsync(
        string query,
        string? sessionToken,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(query) || query.Trim().Length < 3)
        {
            return [];
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{PlacesBaseUrl}places:autocomplete");
        AddHeaders(request, "suggestions.placePrediction.placeId,suggestions.placePrediction.text,suggestions.placePrediction.structuredFormat");
        request.Content = JsonContent.Create(new
        {
            input = query.Trim(),
            includedRegionCodes = new[] { "ci" },
            languageCode = "fr",
            sessionToken = NormalizeSessionToken(sessionToken)
        });

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Google Places autocomplete returned {StatusCode}.", response.StatusCode);
                return [];
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!json.RootElement.TryGetProperty("suggestions", out var suggestions))
            {
                return [];
            }

            return suggestions.EnumerateArray()
                .Select(ReadSuggestion)
                .Where(item => item is not null)
                .Cast<ClientAddressSuggestionResponse>()
                .Take(5)
                .ToArray();
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Google Places autocomplete is temporarily unavailable.");
            return [];
        }
    }

    public async Task<ClientPlaceDetailsResponse?> GetDetailsAsync(
        string placeId,
        string? sessionToken,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(placeId))
        {
            return null;
        }

        var path = $"{PlacesBaseUrl}places/{Uri.EscapeDataString(placeId.Trim())}?languageCode=fr";
        var token = NormalizeSessionToken(sessionToken);
        if (token is not null)
        {
            path += $"&sessionToken={Uri.EscapeDataString(token)}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        AddHeaders(request, "id,formattedAddress,location");

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Google Places details returned {StatusCode}.", response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = json.RootElement;
            if (!root.TryGetProperty("formattedAddress", out var address) ||
                !root.TryGetProperty("location", out var location) ||
                !location.TryGetProperty("latitude", out var latitude) ||
                !location.TryGetProperty("longitude", out var longitude))
            {
                return null;
            }

            return new ClientPlaceDetailsResponse(
                root.TryGetProperty("id", out var id) ? id.GetString() ?? placeId : placeId,
                address.GetString() ?? string.Empty,
                latitude.GetDecimal(),
                longitude.GetDecimal());
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Google Places details are temporarily unavailable.");
            return null;
        }
    }

    private bool IsConfigured => settings.Enabled && !string.IsNullOrWhiteSpace(settings.ApiKey);

    private void AddHeaders(HttpRequestMessage request, string fieldMask)
    {
        request.Headers.Add("X-Goog-Api-Key", settings.ApiKey);
        request.Headers.Add("X-Goog-FieldMask", fieldMask);
    }

    private static string? NormalizeSessionToken(string? sessionToken) =>
        string.IsNullOrWhiteSpace(sessionToken) ? null : sessionToken.Trim();

    private static ClientAddressSuggestionResponse? ReadSuggestion(JsonElement item)
    {
        if (!item.TryGetProperty("placePrediction", out var prediction) ||
            !prediction.TryGetProperty("placeId", out var placeIdElement))
        {
            return null;
        }

        var placeId = placeIdElement.GetString();
        if (string.IsNullOrWhiteSpace(placeId))
        {
            return null;
        }

        var fullText = ReadText(prediction, "text");
        var mainText = string.Empty;
        var secondaryText = string.Empty;
        if (prediction.TryGetProperty("structuredFormat", out var format))
        {
            mainText = ReadText(format, "mainText");
            secondaryText = ReadText(format, "secondaryText");
        }

        return new ClientAddressSuggestionResponse(
            placeId,
            string.IsNullOrWhiteSpace(mainText) ? fullText : mainText,
            secondaryText,
            fullText);
    }

    private static string ReadText(JsonElement parent, string propertyName) =>
        parent.TryGetProperty(propertyName, out var property) &&
        property.TryGetProperty("text", out var text)
            ? text.GetString() ?? string.Empty
            : string.Empty;
}
