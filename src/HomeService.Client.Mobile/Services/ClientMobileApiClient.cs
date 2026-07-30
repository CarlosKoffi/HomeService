using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HomeService.Contracts.Clients;
using HomeService.Contracts.Missions;
using HomeService.Contracts.Notifications;
using HomeService.Contracts.Services;

namespace HomeService.Client.Mobile.Services;

public sealed class ClientMobileApiClient(HttpClient httpClient, ClientSessionStore sessionStore)
{
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

    public async Task<ApiCallResult<IReadOnlyList<ClientMissionListItemResponse>>> GetMissionsAsync(string? status = null, CancellationToken cancellationToken = default)
    {
        var path = string.IsNullOrWhiteSpace(status)
            ? "api/client/missions"
            : $"api/client/missions?status={Uri.EscapeDataString(status)}";

        return await SendWithSessionAsync<IReadOnlyList<ClientMissionListItemResponse>>(HttpMethod.Get, path, body: null, cancellationToken);
    }

    public async Task<ApiCallResult<ClientMissionStatusResponse>> GetMissionAsync(Guid missionId, CancellationToken cancellationToken = default)
    {
        var phoneNumber = sessionStore.GetPhoneNumber();
        var path = $"api/client/missions/{missionId:D}?phoneNumber={Uri.EscapeDataString(phoneNumber ?? string.Empty)}";
        return await SendAsync<ClientMissionStatusResponse>(HttpMethod.Get, path, bearerToken: null, body: null, cancellationToken);
    }

    public async Task<ApiCallResult<CreateClientMissionResponse>> CreateMissionAsync(CreateClientMissionRequest request, CancellationToken cancellationToken = default)
    {
        return await SendAsync<CreateClientMissionResponse>(HttpMethod.Post, "api/client/missions", bearerToken: null, request, cancellationToken);
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

    public async Task<ApiCallResult<ClientAddressResponse>> CreateAddressAsync(UpsertClientAddressRequest request, CancellationToken cancellationToken = default)
    {
        return await SendWithSessionAsync<ClientAddressResponse>(HttpMethod.Post, "api/client/addresses", request, cancellationToken);
    }

    public async Task<ApiCallResult<IReadOnlyList<ClientPaymentMethodResponse>>> GetPaymentMethodsAsync(CancellationToken cancellationToken = default)
    {
        return await SendWithSessionAsync<IReadOnlyList<ClientPaymentMethodResponse>>(HttpMethod.Get, "api/client/payment-methods", body: null, cancellationToken);
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
