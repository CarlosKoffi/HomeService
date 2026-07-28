using System.Net.Http.Headers;
using System.Net.Http.Json;
using HomeService.Contracts.Missions;
using HomeService.Contracts.Notifications;
using HomeService.Contracts.ProviderPortal;

namespace HomeService.Provider.Mobile.Services;

public sealed class ProviderMobileApiClient(HttpClient httpClient)
{
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
