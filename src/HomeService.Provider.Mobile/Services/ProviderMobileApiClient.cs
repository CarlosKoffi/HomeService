using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HomeService.Contracts.Clients;
using HomeService.Contracts.Missions;
using HomeService.Contracts.Notifications;
using HomeService.Contracts.ProviderPortal;
using Microsoft.Maui.Storage;

namespace HomeService.Provider.Mobile.Services;

public sealed class ProviderMobileApiClient(HttpClient httpClient)
{
    private const long MaxUploadBytes = 25L * 1024 * 1024;

    public Task<ApiCallResult<ProviderInvitationPreviewResponse>> GetInvitationAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ProviderInvitationPreviewResponse>(
            HttpMethod.Get,
            $"api/provider-portal/invitations/{Uri.EscapeDataString(code)}",
            bearerToken: null,
            body: null,
            cancellationToken);
    }

    public Task<ApiCallResult<ProviderPortalLoginResponse>> ActivateAsync(
        ProviderInvitationActivationRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ProviderPortalLoginResponse>(
            HttpMethod.Post,
            "api/provider-portal/activate",
            bearerToken: null,
            request,
            cancellationToken);
    }

    public Task<ApiCallResult<ProviderPortalLoginResponse>> LoginAsync(
        ProviderPortalLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ProviderPortalLoginResponse>(
            HttpMethod.Post,
            "api/provider-portal/login",
            bearerToken: null,
            request,
            cancellationToken);
    }

    public Task<ApiCallResult<ProviderPortalMeResponse>> GetMeAsync(
        string bearerToken,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ProviderPortalMeResponse>(
            HttpMethod.Get,
            "api/provider-portal/me",
            bearerToken,
            body: null,
            cancellationToken);
    }

    public Task<ApiCallResult<MobileDeviceTokenResponse>> RegisterDeviceTokenAsync(
        string bearerToken,
        RegisterMobileDeviceTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<MobileDeviceTokenResponse>(
            HttpMethod.Post,
            "api/provider-portal/mobile/device-token",
            bearerToken,
            request,
            cancellationToken);
    }

    public async Task<ProviderMobileHomeResponse?> GetHomeAsync(string bearerToken, CancellationToken cancellationToken = default)
    {
        var result = await GetHomeResultAsync(bearerToken, cancellationToken);
        return result.Response;
    }

    public Task<ApiCallResult<ProviderMobileHomeResponse>> GetHomeResultAsync(
        string bearerToken,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ProviderMobileHomeResponse>(
            HttpMethod.Get,
            "api/provider-portal/mobile/home",
            bearerToken,
            body: null,
            cancellationToken);
    }

    public Task<ApiCallResult<ProviderMobileProfileResponse>> GetProfileAsync(
        string bearerToken,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ProviderMobileProfileResponse>(
            HttpMethod.Get,
            "api/provider-portal/mobile/profile",
            bearerToken,
            body: null,
            cancellationToken);
    }

    public Task<ApiCallResult<ProviderMobileProfileResponse>> UpdateProfileAsync(
        string bearerToken,
        UpdateProviderMobileProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ProviderMobileProfileResponse>(
            HttpMethod.Put,
            "api/provider-portal/mobile/profile",
            bearerToken,
            request,
            cancellationToken);
    }

    public Task<ApiCallResult<IReadOnlyList<ClientAddressSuggestionResponse>>> AutocompleteAddressAsync(
        string bearerToken,
        string query,
        string sessionToken,
        CancellationToken cancellationToken = default)
    {
        var path = $"api/provider-portal/mobile/addresses/autocomplete?query={Uri.EscapeDataString(query)}&sessionToken={Uri.EscapeDataString(sessionToken)}";
        return SendAsync<IReadOnlyList<ClientAddressSuggestionResponse>>(
            HttpMethod.Get,
            path,
            bearerToken,
            body: null,
            cancellationToken);
    }

    public Task<ApiCallResult<ClientPlaceDetailsResponse>> GetPlaceDetailsAsync(
        string bearerToken,
        string placeId,
        string sessionToken,
        CancellationToken cancellationToken = default)
    {
        var path = $"api/provider-portal/mobile/addresses/places/{Uri.EscapeDataString(placeId)}?sessionToken={Uri.EscapeDataString(sessionToken)}";
        return SendAsync<ClientPlaceDetailsResponse>(
            HttpMethod.Get,
            path,
            bearerToken,
            body: null,
            cancellationToken);
    }

    public Task<ApiCallResult<ProviderMobileAvailabilityResponse>> UpdateAvailabilityAsync(
        string bearerToken,
        UpdateProviderMobileAvailabilityRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ProviderMobileAvailabilityResponse>(
            HttpMethod.Put,
            "api/provider-portal/mobile/availability",
            bearerToken,
            request,
            cancellationToken);
    }

    public Task<ApiCallResult<ProviderMobileMissionListResponse>> GetMissionsAsync(
        string bearerToken,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (from is not null)
        {
            query.Add($"from={Uri.EscapeDataString(from.Value.ToString("O"))}");
        }

        if (to is not null)
        {
            query.Add($"to={Uri.EscapeDataString(to.Value.ToString("O"))}");
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query.Add($"status={Uri.EscapeDataString(status)}");
        }

        var suffix = query.Count == 0 ? string.Empty : $"?{string.Join("&", query)}";
        return SendAsync<ProviderMobileMissionListResponse>(
            HttpMethod.Get,
            $"api/provider-portal/mobile/missions{suffix}",
            bearerToken,
            body: null,
            cancellationToken);
    }

    public Task<ApiCallResult<ProviderMobileNotificationListResponse>> GetNotificationsAsync(
        string bearerToken,
        bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ProviderMobileNotificationListResponse>(
            HttpMethod.Get,
            $"api/provider-portal/mobile/notifications?unreadOnly={unreadOnly.ToString().ToLowerInvariant()}",
            bearerToken,
            body: null,
            cancellationToken);
    }

    public Task<ApiCallResult<MobileNavigationBadgeResponse>> GetNavigationBadgesAsync(
        string bearerToken,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<MobileNavigationBadgeResponse>(
            HttpMethod.Get,
            "api/provider-portal/mobile/navigation-badges",
            bearerToken,
            body: null,
            cancellationToken);
    }

    public Task<ApiCallResult<bool>> MarkNotificationReadAsync(
        string bearerToken,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        return SendWithoutResponseAsync(
            HttpMethod.Post,
            $"api/provider-portal/mobile/notifications/{notificationId:D}/read",
            bearerToken,
            body: null,
            cancellationToken);
    }

    public Task<ApiCallResult<ProviderMobileMissionDetailResponse>> GetMissionDetailAsync(
        string bearerToken,
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ProviderMobileMissionDetailResponse>(
            HttpMethod.Get,
            $"api/provider-portal/mobile/mission-assignments/{assignmentId:D}",
            bearerToken,
            body: null,
            cancellationToken);
    }

    public Task<ApiCallResult<ProviderMobileProfileDocumentResponse>> UploadDocumentAsync(
        string bearerToken,
        string documentType,
        FileResult file,
        CancellationToken cancellationToken = default)
    {
        return SendMultipartAsync<ProviderMobileProfileDocumentResponse>(
            "api/provider-portal/mobile/profile/documents",
            bearerToken,
            file,
            new Dictionary<string, string> { ["documentType"] = documentType },
            cancellationToken);
    }

    public Task<ApiCallResult<ProviderMobilePortfolioUploadResponse>> UploadPortfolioAsync(
        string bearerToken,
        Guid serviceId,
        FileResult file,
        CancellationToken cancellationToken = default)
    {
        return SendMultipartAsync<ProviderMobilePortfolioUploadResponse>(
            "api/provider-portal/mobile/profile/portfolio",
            bearerToken,
            file,
            new Dictionary<string, string> { ["serviceId"] = serviceId.ToString("D") },
            cancellationToken);
    }

    public Task<ApiCallResult<ProviderLocationVerificationResponse>> AcceptMissionAsync(
        string bearerToken,
        Guid assignmentId,
        ProviderAcceptMissionRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ProviderLocationVerificationResponse>(
            HttpMethod.Post,
            $"api/provider-portal/mobile/mission-assignments/{assignmentId:D}/accept",
            bearerToken,
            request,
            cancellationToken);
    }

    public Task<ApiCallResult<ProviderLocationVerificationResponse>> RefuseMissionAsync(
        string bearerToken,
        Guid assignmentId,
        ProviderRefuseMissionRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ProviderLocationVerificationResponse>(
            HttpMethod.Post,
            $"api/provider-portal/mobile/mission-assignments/{assignmentId:D}/refuse",
            bearerToken,
            request,
            cancellationToken);
    }

    public Task<ApiCallResult<ProviderLocationVerificationResponse>> VerifyArrivalAsync(
        string bearerToken,
        Guid assignmentId,
        ProviderLocationVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ProviderLocationVerificationResponse>(
            HttpMethod.Post,
            $"api/provider-portal/mobile/mission-assignments/{assignmentId:D}/verify-arrival",
            bearerToken,
            request,
            cancellationToken);
    }

    public Task<ApiCallResult<ProviderLocationVerificationResponse>> MarkMissionOnTheWayAsync(
        string bearerToken,
        Guid assignmentId,
        ProviderLocationVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ProviderLocationVerificationResponse>(
            HttpMethod.Post,
            $"api/provider-portal/mobile/mission-assignments/{assignmentId:D}/on-the-way",
            bearerToken,
            request,
            cancellationToken);
    }

    public Task<ApiCallResult<ProviderLocationVerificationResponse>> UpdateMissionLocationAsync(
        string bearerToken,
        Guid assignmentId,
        ProviderLocationVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ProviderLocationVerificationResponse>(
            HttpMethod.Post,
            $"api/provider-portal/mobile/mission-assignments/{assignmentId:D}/location",
            bearerToken,
            request,
            cancellationToken);
    }

    public Task<ApiCallResult<ProviderLocationVerificationResponse>> StartMissionAsync(
        string bearerToken,
        Guid assignmentId,
        ProviderLocationVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ProviderLocationVerificationResponse>(
            HttpMethod.Post,
            $"api/provider-portal/mobile/mission-assignments/{assignmentId:D}/start",
            bearerToken,
            request,
            cancellationToken);
    }

    public Task<ApiCallResult<ProviderLocationVerificationResponse>> CompleteMissionAsync(
        string bearerToken,
        Guid assignmentId,
        ProviderCompleteMissionRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ProviderLocationVerificationResponse>(
            HttpMethod.Post,
            $"api/provider-portal/mobile/mission-assignments/{assignmentId:D}/complete",
            bearerToken,
            request,
            cancellationToken);
    }

    public Task<ApiCallResult<ProviderMissionChatResponse>> GetMissionMessagesAsync(
        string bearerToken,
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ProviderMissionChatResponse>(
            HttpMethod.Get,
            $"api/provider-portal/mobile/mission-assignments/{assignmentId:D}/messages",
            bearerToken,
            body: null,
            cancellationToken);
    }

    public Task<ApiCallResult<SendProviderMissionMessageResponse>> SendMissionMessageAsync(
        string bearerToken,
        Guid assignmentId,
        SendProviderMissionMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<SendProviderMissionMessageResponse>(
            HttpMethod.Post,
            $"api/provider-portal/mobile/mission-assignments/{assignmentId:D}/messages",
            bearerToken,
            request,
            cancellationToken);
    }

    public Task<ApiCallResult<MissionAdditionalQuoteResponse>> RequestAdditionalQuoteAsync(
        string bearerToken,
        Guid assignmentId,
        RequestMissionAdditionalQuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<MissionAdditionalQuoteResponse>(
            HttpMethod.Post,
            $"api/provider-portal/mobile/mission-assignments/{assignmentId:D}/additional-quotes/request",
            bearerToken,
            request,
            cancellationToken);
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
            return ApiCallResult<TResponse>.Failed(0, "Connexion trop lente. Reessayez dans quelques instants.");
        }
        catch (JsonException)
        {
            return ApiCallResult<TResponse>.Failed(0, "La reponse du serveur est invalide. Reessayez dans quelques instants.");
        }
        catch (Exception exception) when (exception is HttpRequestException
            or IOException
            or ObjectDisposedException
            or InvalidCastException)
        {
            return ApiCallResult<TResponse>.Failed(0, "Connexion impossible. Verifiez votre reseau.");
        }
    }

    public async Task<ApiCallResult<byte[]>> DownloadAsync(
        string bearerToken,
        string path,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiCallResult<byte[]>.Failed((int)response.StatusCode, "Image indisponible.");
            }

            return ApiCallResult<byte[]>.Ok(await response.Content.ReadAsByteArrayAsync(cancellationToken));
        }
        catch (HttpRequestException)
        {
            return ApiCallResult<byte[]>.Failed(0, "Connexion impossible.");
        }
    }

    private async Task<ApiCallResult<bool>> SendWithoutResponseAsync(
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
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync(cancellationToken);
                return ApiCallResult<bool>.Failed((int)response.StatusCode, NormalizeErrorMessage(message));
            }

            return ApiCallResult<bool>.Ok(true);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiCallResult<bool>.Failed(0, "Connexion trop lente. Reessayez dans quelques instants.");
        }
        catch (HttpRequestException)
        {
            return ApiCallResult<bool>.Failed(0, "Connexion impossible. Verifiez votre reseau.");
        }
    }

    private async Task<ApiCallResult<TResponse>> SendMultipartAsync<TResponse>(
        string path,
        string bearerToken,
        FileResult file,
        IReadOnlyDictionary<string, string> fields,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return ApiCallResult<TResponse>.Failed(401, "Votre session a expiré. Reconnectez-vous pour continuer.");
        }

        try
        {
            await using var stream = await file.OpenReadAsync();
            if (stream.CanSeek && (stream.Length == 0 || stream.Length > MaxUploadBytes))
            {
                return ApiCallResult<TResponse>.Failed(0, "Le fichier doit faire entre 1 octet et 25 Mo.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            using var content = new MultipartFormDataContent();
            foreach (var field in fields)
            {
                content.Add(new StringContent(field.Value), field.Key);
            }

            var safeContentType = NormalizeUploadContentType(file.ContentType, file.FileName);
            var safeFileName = NormalizeUploadFileName(file.FileName, safeContentType);
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(safeContentType);
            content.Add(fileContent, "file", safeFileName);
            request.Content = content;

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync(cancellationToken);
                return ApiCallResult<TResponse>.Failed((int)response.StatusCode, NormalizeErrorMessage(message));
            }

            var payload = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);
            return payload is null
                ? ApiCallResult<TResponse>.Failed((int)response.StatusCode, "Réponse vide du serveur.")
                : ApiCallResult<TResponse>.Ok(payload);
        }
        catch (HttpRequestException)
        {
            return ApiCallResult<TResponse>.Failed(0, "Connexion impossible. Vérifiez votre réseau.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiCallResult<TResponse>.Failed(0, "L'envoi prend trop de temps. Vérifiez votre réseau puis réessayez.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ApiCallResult<TResponse>.Failed(0, "Le fichier ne peut pas être lu sur cet appareil.");
        }
    }

    private static string NormalizeUploadContentType(string? contentType, string fileName)
    {
        var normalized = contentType?.Split(';', 2)[0].Trim().ToLowerInvariant();
        if (normalized is "image/jpeg" or "image/png" or "image/webp" or "image/heic" or "image/heif" or "application/pdf")
        {
            return normalized;
        }

        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".heic" => "image/heic",
            ".heif" => "image/heif",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
    }

    private static string NormalizeUploadFileName(string fileName, string contentType)
    {
        var safeName = Path.GetFileName(fileName);
        if (!string.IsNullOrWhiteSpace(safeName) && !string.IsNullOrWhiteSpace(Path.GetExtension(safeName)))
        {
            return safeName;
        }

        var extension = contentType switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/heic" => ".heic",
            "image/heif" => ".heif",
            "application/pdf" => ".pdf",
            _ => ".jpg"
        };
        return $"fichier-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}{extension}";
    }

    private static string NormalizeErrorMessage(string rawMessage)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
        {
            return "Action impossible pour le moment.";
        }

        return rawMessage.Length > 260 ? rawMessage[..260] : rawMessage;
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
