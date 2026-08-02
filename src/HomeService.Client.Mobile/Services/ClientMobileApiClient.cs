using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Collections.Concurrent;
using System.Text.Json;
using HomeService.Contracts.Clients;
using HomeService.Contracts.Missions;
using HomeService.Contracts.Notifications;
using HomeService.Contracts.Services;

namespace HomeService.Client.Mobile.Services;

public sealed class ClientMobileApiClient(HttpClient httpClient, ClientSessionStore sessionStore)
{
    private static readonly ConcurrentDictionary<string, Lazy<Task<byte[]?>>> MediaCache = new(StringComparer.OrdinalIgnoreCase);
    public Task<ApiCallResult<ClientAuthResponse>> RegisterAsync(RegisterClientRequest request, CancellationToken cancellationToken = default)
    {
        return SendAsync<ClientAuthResponse>(HttpMethod.Post, "api/client/auth/register", bearerToken: null, request, cancellationToken);
    }

    public Task<ApiCallResult<ClientAuthResponse>> LoginAsync(LoginClientRequest request, CancellationToken cancellationToken = default)
    {
        return SendAsync<ClientAuthResponse>(HttpMethod.Post, "api/client/auth/login", bearerToken: null, request, cancellationToken);
    }

    public async Task<ApiCallResult<ClientMeResponse>> GetMeAsync(CancellationToken cancellationToken = default)
    {
        return await SendWithSessionAsync<ClientMeResponse>(HttpMethod.Get, "api/client/me", body: null, cancellationToken);
    }

    public async Task<ApiCallResult<ClientMeResponse>> UpdateMeAsync(UpdateClientProfileRequest request, CancellationToken cancellationToken = default)
    {
        return await SendWithSessionAsync<ClientMeResponse>(HttpMethod.Put, "api/client/me", request, cancellationToken);
    }

    public async Task<ApiCallResult<ClientProfilePhotoResponse>> UploadProfilePhotoAsync(
        FileResult file,
        CancellationToken cancellationToken = default)
    {
        var token = await sessionStore.GetTokenAsync();
        await using var stream = await file.OpenReadAsync();
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetImageContentType(file.FileName));
        content.Add(fileContent, "photo", file.FileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/client/me/photo") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiCallResult<ClientProfilePhotoResponse>.Failed(
                    (int)response.StatusCode,
                    NormalizeErrorMessage(await response.Content.ReadAsStringAsync(cancellationToken)));
            }

            var payload = await response.Content.ReadFromJsonAsync<ClientProfilePhotoResponse>(cancellationToken);
            return payload is null
                ? ApiCallResult<ClientProfilePhotoResponse>.Failed((int)response.StatusCode, "Reponse vide du serveur.")
                : ApiCallResult<ClientProfilePhotoResponse>.Ok(payload);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException)
        {
            return ApiCallResult<ClientProfilePhotoResponse>.Failed(0, "Envoi impossible. Verifiez votre connexion puis reessayez.");
        }
    }

    public async Task<ImageSource?> DownloadProfilePhotoAsync(
        string? url,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var token = await sessionStore.GetTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return bytes.Length == 0 ? null : ImageSource.FromStream(() => new MemoryStream(bytes, writable: false));
    }

    public async Task<ApiCallResult<MobileDeviceTokenResponse>> RegisterDeviceTokenAsync(
        RegisterMobileDeviceTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        return await SendWithSessionAsync<MobileDeviceTokenResponse>(
            HttpMethod.Post,
            "api/client/mobile/device-token",
            request,
            cancellationToken);
    }

    public async Task<ApiCallResult<IReadOnlyList<ServiceSummaryResponse>>> GetServicesAsync(CancellationToken cancellationToken = default)
    {
        return await SendAsync<IReadOnlyList<ServiceSummaryResponse>>(HttpMethod.Get, "api/services", bearerToken: null, body: null, cancellationToken);
    }

    public async Task<ApiCallResult<IReadOnlyList<ClientCatalogSearchResultResponse>>> SearchCatalogAsync(string query, CancellationToken cancellationToken = default)
    {
        var path = $"api/client/catalog/search?q={Uri.EscapeDataString(query)}";
        return await SendAsync<IReadOnlyList<ClientCatalogSearchResultResponse>>(HttpMethod.Get, path, bearerToken: null, body: null, cancellationToken);
    }

    public async Task<ApiCallResult<PrepareClientMissionResponse>> PrepareMissionAsync(PrepareClientMissionRequest request, CancellationToken cancellationToken = default)
    {
        return await SendAsync<PrepareClientMissionResponse>(HttpMethod.Post, "api/client/missions/prepare", bearerToken: null, request, cancellationToken);
    }

    public string? ToAbsoluteMediaUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            return url;
        }

        return new Uri(httpClient.BaseAddress!, url.TrimStart('/')).ToString();
    }

    public ImageSource? ToRemoteImageSource(string? url)
    {
        var absoluteUrl = ToAbsoluteMediaUrl(url);
        if (absoluteUrl is null || !Uri.TryCreate(absoluteUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return new UriImageSource
        {
            Uri = uri,
            CachingEnabled = true,
            CacheValidity = TimeSpan.FromDays(1)
        };
    }

    public async Task<ImageSource?> DownloadMediaImageSourceAsync(
        string? url,
        CancellationToken cancellationToken = default)
    {
        var absoluteUrl = ToAbsoluteMediaUrl(url);
        if (absoluteUrl is null)
        {
            return null;
        }

        try
        {
            var lazyBytes = MediaCache.GetOrAdd(
                absoluteUrl,
                key => new Lazy<Task<byte[]?>>(
                    () => DownloadMediaBytesAsync(key),
                    LazyThreadSafetyMode.ExecutionAndPublication));
            var bytes = await lazyBytes.Value.WaitAsync(cancellationToken);
            return bytes is null || bytes.Length == 0
                ? null
                : ImageSource.FromStream(() => new MemoryStream(bytes, writable: false));
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException)
        {
            return null;
        }

        async Task<byte[]?> DownloadMediaBytesAsync(string mediaUrl)
        {
            try
            {
                return await httpClient.GetByteArrayAsync(mediaUrl);
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException)
            {
                MediaCache.TryRemove(mediaUrl, out _);
                return null;
            }
        }
    }

    public async Task<ApiCallResult<IReadOnlyList<ClientMissionListItemResponse>>> GetMissionsAsync(string? status = null, CancellationToken cancellationToken = default)
    {
        var path = string.IsNullOrWhiteSpace(status)
            ? "api/client/missions"
            : $"api/client/missions?status={Uri.EscapeDataString(status)}";

        return await SendWithSessionAsync<IReadOnlyList<ClientMissionListItemResponse>>(HttpMethod.Get, path, body: null, cancellationToken);
    }

    public async Task<ApiCallResult<ClientMissionStatusResponse>> GetMissionAsync(Guid missionId, CancellationToken cancellationToken = default)
    {
        return await SendWithSessionAsync<ClientMissionStatusResponse>(HttpMethod.Get, $"api/client/missions/{missionId:D}", body: null, cancellationToken);
    }

    public async Task<ImageSource?> DownloadMissionAttachmentImageSourceAsync(
        Guid missionId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        var token = await sessionStore.GetTokenAsync();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/client/missions/{missionId:D}/attachments/{attachmentId:D}/preview");
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return bytes.Length == 0
                ? null
                : ImageSource.FromStream(() => new MemoryStream(bytes, writable: false));
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException)
        {
            return null;
        }
    }

    public Task<ApiCallResult<ClientMissionScreenResponse>> GetMissionScreenAsync(Guid missionId, CancellationToken cancellationToken = default)
        => SendWithSessionAsync<ClientMissionScreenResponse>(HttpMethod.Get, $"api/client/missions/{missionId:D}/screen", body: null, cancellationToken);

    public async Task<ApiCallResult<CreateClientMissionResponse>> CreateMissionAsync(CreateClientMissionRequest request, CancellationToken cancellationToken = default)
    {
        return await SendWithSessionAsync<CreateClientMissionResponse>(HttpMethod.Post, "api/client/missions", request, cancellationToken);
    }

    public async Task<ApiCallResult<ClientMissionPhotoUploadResponse>> UploadMissionPhotoAsync(
        FileResult file,
        string? caption,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var stream = await file.OpenReadAsync();
            using var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(file.ContentType)
                ? "image/jpeg"
                : file.ContentType);
            content.Add(fileContent, "photo", file.FileName);

            if (!string.IsNullOrWhiteSpace(caption))
            {
                content.Add(new StringContent(caption), "caption");
            }

            using var response = await httpClient.PostAsync("api/client/mission-photos", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync(cancellationToken);
                return ApiCallResult<ClientMissionPhotoUploadResponse>.Failed((int)response.StatusCode, NormalizeErrorMessage(message));
            }

            var payload = await response.Content.ReadFromJsonAsync<ClientMissionPhotoUploadResponse>(cancellationToken);
            return payload is null
                ? ApiCallResult<ClientMissionPhotoUploadResponse>.Failed((int)response.StatusCode, "Reponse vide du serveur.")
                : ApiCallResult<ClientMissionPhotoUploadResponse>.Ok(payload);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiCallResult<ClientMissionPhotoUploadResponse>.Failed(0, "Upload trop lent. Reessayez avec un meilleur reseau.");
        }
        catch (Exception exception) when (exception is IOException or HttpRequestException or UnauthorizedAccessException)
        {
            return ApiCallResult<ClientMissionPhotoUploadResponse>.Failed(0, "Impossible d'envoyer la photo.");
        }
    }

    public async Task<ApiCallResult<ConfirmClientMissionResponse>> ConfirmMissionAsync(
        Guid missionId,
        string? paymentReference,
        CancellationToken cancellationToken = default)
    {
        var request = new ConfirmClientMissionRequest(sessionStore.GetPhoneNumber() ?? string.Empty, paymentReference);
        return await SendAsync<ConfirmClientMissionResponse>(HttpMethod.Post, $"api/client/missions/{missionId:D}/confirm", bearerToken: null, request, cancellationToken);
    }

    public async Task<ApiCallResult<CancelClientMissionResponse>> CancelMissionAsync(
        Guid missionId,
        string reason,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        var request = new CancelClientMissionRequest(sessionStore.GetPhoneNumber() ?? string.Empty, reason, comment);
        return await SendAsync<CancelClientMissionResponse>(HttpMethod.Post, $"api/client/missions/{missionId:D}/cancel", bearerToken: null, request, cancellationToken);
    }

    public async Task<ApiCallResult<ValidateClientMissionCompletionResponse>> ValidateCompletionAsync(
        Guid missionId,
        int rating,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        var request = new ValidateClientMissionCompletionRequest(
            sessionStore.GetPhoneNumber() ?? string.Empty,
            rating,
            rating,
            rating,
            rating,
            comment,
            PayoutReference: null);

        return await SendAsync<ValidateClientMissionCompletionResponse>(HttpMethod.Post, $"api/client/missions/{missionId:D}/validate-completion", bearerToken: null, request, cancellationToken);
    }

    public async Task<ApiCallResult<MissionAdditionalQuoteResponse>> PayAdditionalQuoteAsync(
        Guid missionId,
        Guid quoteId,
        string? paymentReference,
        CancellationToken cancellationToken = default)
    {
        var request = new PayMissionAdditionalQuoteRequest(sessionStore.GetPhoneNumber() ?? string.Empty, paymentReference);
        return await SendAsync<MissionAdditionalQuoteResponse>(
            HttpMethod.Post,
            $"api/client/missions/{missionId:D}/additional-quotes/{quoteId:D}/pay",
            bearerToken: null,
            request,
            cancellationToken);
    }

    public async Task<ApiCallResult<ClientMissionChatResponse>> GetMissionMessagesAsync(Guid missionId, CancellationToken cancellationToken = default)
    {
        var phoneNumber = sessionStore.GetPhoneNumber();
        var path = $"api/client/missions/{missionId:D}/messages?phoneNumber={Uri.EscapeDataString(phoneNumber ?? string.Empty)}";
        return await SendAsync<ClientMissionChatResponse>(HttpMethod.Get, path, bearerToken: null, body: null, cancellationToken);
    }

    public async Task<ApiCallResult<SendClientMissionMessageResponse>> SendMissionMessageAsync(Guid missionId, string body, CancellationToken cancellationToken = default)
    {
        var request = new SendClientMissionMessageRequest(sessionStore.GetPhoneNumber() ?? string.Empty, body, null, null);
        return await SendAsync<SendClientMissionMessageResponse>(HttpMethod.Post, $"api/client/missions/{missionId:D}/messages", bearerToken: null, request, cancellationToken);
    }

    public async Task<ApiCallResult<IReadOnlyList<ClientAddressResponse>>> GetAddressesAsync(CancellationToken cancellationToken = default)
    {
        return await SendWithSessionAsync<IReadOnlyList<ClientAddressResponse>>(HttpMethod.Get, "api/client/addresses", body: null, cancellationToken);
    }

    public async Task<ApiCallResult<IReadOnlyList<ClientAddressSuggestionResponse>>> AutocompleteAddressAsync(
        string query,
        string sessionToken,
        CancellationToken cancellationToken = default)
    {
        var path = $"api/client/addresses/autocomplete?query={Uri.EscapeDataString(query)}&sessionToken={Uri.EscapeDataString(sessionToken)}";
        return await SendWithSessionAsync<IReadOnlyList<ClientAddressSuggestionResponse>>(HttpMethod.Get, path, body: null, cancellationToken);
    }

    public async Task<ApiCallResult<ClientPlaceDetailsResponse>> GetPlaceDetailsAsync(
        string placeId,
        string sessionToken,
        CancellationToken cancellationToken = default)
    {
        var path = $"api/client/addresses/places/{Uri.EscapeDataString(placeId)}?sessionToken={Uri.EscapeDataString(sessionToken)}";
        return await SendWithSessionAsync<ClientPlaceDetailsResponse>(HttpMethod.Get, path, body: null, cancellationToken);
    }

    public async Task<ApiCallResult<ClientAddressResponse>> CreateAddressAsync(UpsertClientAddressRequest request, CancellationToken cancellationToken = default)
    {
        return await SendWithSessionAsync<ClientAddressResponse>(HttpMethod.Post, "api/client/addresses", request, cancellationToken);
    }

    public async Task<ApiCallResult<ClientAddressResponse>> UpdateAddressAsync(Guid addressId, UpsertClientAddressRequest request, CancellationToken cancellationToken = default)
    {
        return await SendWithSessionAsync<ClientAddressResponse>(HttpMethod.Put, $"api/client/addresses/{addressId:D}", request, cancellationToken);
    }

    public async Task<ApiCallResult<object>> DeleteAddressAsync(Guid addressId, CancellationToken cancellationToken = default)
    {
        return await SendWithSessionAsync<object>(HttpMethod.Delete, $"api/client/addresses/{addressId:D}", body: null, cancellationToken);
    }

    public async Task<ApiCallResult<ClientNotificationListResponse>> GetNotificationsAsync(bool unreadOnly = false, CancellationToken cancellationToken = default)
    {
        return await SendWithSessionAsync<ClientNotificationListResponse>(HttpMethod.Get, $"api/client/notifications?unreadOnly={unreadOnly.ToString().ToLowerInvariant()}", body: null, cancellationToken);
    }

    public async Task<ApiCallResult<object>> MarkNotificationReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        return await SendWithSessionAsync<object>(HttpMethod.Post, $"api/client/notifications/{notificationId:D}/mark-read", body: null, cancellationToken);
    }

    public async Task<ApiCallResult<IReadOnlyList<ClientPaymentMethodResponse>>> GetPaymentMethodsAsync(CancellationToken cancellationToken = default)
    {
        return await SendWithSessionAsync<IReadOnlyList<ClientPaymentMethodResponse>>(HttpMethod.Get, "api/client/payment-methods", body: null, cancellationToken);
    }

    public async Task<ApiCallResult<IReadOnlyList<PaymentProviderResponse>>> GetPaymentProvidersAsync(CancellationToken cancellationToken = default)
    {
        return await SendAsync<IReadOnlyList<PaymentProviderResponse>>(HttpMethod.Get, "api/client/payment-providers", bearerToken: null, body: null, cancellationToken);
    }

    public async Task<ApiCallResult<ClientPaymentMethodResponse>> CreatePaymentMethodAsync(
        UpsertClientPaymentMethodRequest request,
        CancellationToken cancellationToken = default)
    {
        return await SendWithSessionAsync<ClientPaymentMethodResponse>(HttpMethod.Post, "api/client/payment-methods", request, cancellationToken);
    }

    public async Task<ApiCallResult<bool>> DeletePaymentMethodAsync(
        Guid paymentMethodId,
        CancellationToken cancellationToken = default)
    {
        var token = await sessionStore.GetTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/client/payment-methods/{paymentMethodId:D}");
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync(cancellationToken);
                return ApiCallResult<bool>.Failed((int)response.StatusCode, NormalizeErrorMessage(message));
            }

            return ApiCallResult<bool>.Ok(true);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiCallResult<bool>.Failed(0, "Connexion trop lente. Reessayez avec un meilleur reseau.");
        }
        catch (HttpRequestException)
        {
            return ApiCallResult<bool>.Failed(0, "Connexion impossible. Verifiez votre reseau.");
        }
    }

    public async Task<ApiCallResult<ClientMissionPaymentSelectionResponse>> SelectMissionPaymentMethodAsync(
        Guid missionId,
        Guid paymentMethodId,
        CancellationToken cancellationToken = default)
    {
        return await SendWithSessionAsync<ClientMissionPaymentSelectionResponse>(
            HttpMethod.Put,
            $"api/client/missions/{missionId:D}/payment-method",
            new SelectClientMissionPaymentMethodRequest(paymentMethodId),
            cancellationToken);
    }

    public async Task<ApiCallResult<byte[]>> DownloadMissionInvoiceAsync(
        Guid missionId,
        CancellationToken cancellationToken = default)
    {
        var token = await sessionStore.GetTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/client/missions/{missionId:D}/invoice");
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync(cancellationToken);
                return ApiCallResult<byte[]>.Failed((int)response.StatusCode, NormalizeErrorMessage(message));
            }

            return ApiCallResult<byte[]>.Ok(await response.Content.ReadAsByteArrayAsync(cancellationToken));
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiCallResult<byte[]>.Failed(0, "Connexion trop lente. Reessayez avec un meilleur reseau.");
        }
        catch (HttpRequestException)
        {
            return ApiCallResult<byte[]>.Failed(0, "Connexion impossible. Verifiez votre reseau.");
        }
    }

    private async Task<ApiCallResult<TResponse>> SendWithSessionAsync<TResponse>(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        var token = await sessionStore.GetTokenAsync();
        return await SendAsync<TResponse>(method, path, token, body, cancellationToken);
    }

    private async Task<ApiCallResult<TResponse>> SendAsync<TResponse>(
        HttpMethod method,
        string path,
        string? bearerToken,
        object? body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync(cancellationToken);
                return ApiCallResult<TResponse>.Failed((int)response.StatusCode, NormalizeErrorMessage(message));
            }

            var payload = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);
            return payload is null
                ? ApiCallResult<TResponse>.Failed((int)response.StatusCode, "Reponse vide du serveur.")
                : ApiCallResult<TResponse>.Ok(payload);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiCallResult<TResponse>.Failed(0, "Connexion trop lente. Reessayez avec un meilleur reseau.");
        }
        catch (HttpRequestException)
        {
            return ApiCallResult<TResponse>.Failed(0, "Connexion impossible. Verifiez votre reseau.");
        }
    }

    private static string NormalizeErrorMessage(string rawMessage)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
        {
            return "Action impossible pour le moment.";
        }

        try
        {
            using var document = JsonDocument.Parse(rawMessage);
            if (document.RootElement.TryGetProperty("errors", out var errors)
                && errors.ValueKind == JsonValueKind.Array)
            {
                var messages = errors
                    .EnumerateArray()
                    .Select(error => error.GetString())
                    .Where(error => !string.IsNullOrWhiteSpace(error))
                    .ToList();

                if (messages.Count > 0)
                {
                    return Trim(string.Join(Environment.NewLine, messages));
                }
            }

            if (document.RootElement.TryGetProperty("message", out var message))
            {
                return Trim(message.GetString());
            }

            if (document.RootElement.TryGetProperty("detail", out var detail))
            {
                return Trim(detail.GetString());
            }
        }
        catch (JsonException)
        {
        }

        return Trim(rawMessage);
    }

    private static string Trim(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Action impossible pour le moment.";
        }

        return message.Length > 220 ? message[..220] : message;
    }

    private static string GetImageContentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".heic" => "image/heic",
        ".heif" => "image/heif",
        _ => "image/jpeg"
    };
}

public sealed record ApiCallResult<TResponse>(
    bool IsSuccess,
    int StatusCode,
    string? ErrorMessage,
    TResponse? Response)
{
    public static ApiCallResult<TResponse> Ok(TResponse response)
    {
        return new ApiCallResult<TResponse>(true, 200, null, response);
    }

    public static ApiCallResult<TResponse> Failed(int statusCode, string errorMessage)
    {
        return new ApiCallResult<TResponse>(false, statusCode, errorMessage, default);
    }
}
