using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HomeService.Contracts.CompanyPortal;
using HomeService.Contracts.Notifications;
using HomeService.Contracts.Missions;

namespace HomeService.Company.Mobile.Services;

public sealed class CompanyMobileApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<ApiCallResult<CompanyPortalLoginResponse>> LoginAsync(
        CompanyPortalLoginRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<CompanyPortalLoginResponse>(HttpMethod.Post, "api/company-portal/login", null, request, cancellationToken);

    public Task<ApiCallResult<IReadOnlyList<CompanyPortalMissionResponse>>> GetMissionsAsync(
        string token,
        Guid companyId,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<CompanyPortalMissionResponse>>(
            HttpMethod.Get,
            $"api/company-portal/{companyId:D}/missions?view=all",
            token,
            null,
            cancellationToken);

    public Task<ApiCallResult<CompanyPortalMissionDetailResponse>> GetMissionDetailAsync(
        string token,
        Guid companyId,
        Guid missionId,
        CancellationToken cancellationToken = default)
        => SendAsync<CompanyPortalMissionDetailResponse>(
            HttpMethod.Get,
            $"api/company-portal/{companyId:D}/missions/{missionId:D}",
            token,
            null,
            cancellationToken);

    public Task<ApiCallResult<IReadOnlyList<CompanyMissionOfferResponse>>> GetOffersAsync(
        string token,
        Guid companyId,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<CompanyMissionOfferResponse>>(
            HttpMethod.Get,
            $"api/company-portal/{companyId:D}/mission-offers",
            token,
            null,
            cancellationToken);

    public Task<ApiCallResult<bool>> AcceptOfferAsync(
        string token,
        Guid companyId,
        Guid offerId,
        CancellationToken cancellationToken = default)
        => SendWithoutBodyAsync(
            HttpMethod.Post,
            $"api/company-portal/{companyId:D}/mission-offers/{offerId:D}/accept",
            token,
            null,
            cancellationToken);

    public Task<ApiCallResult<bool>> RefuseOfferAsync(
        string token,
        Guid companyId,
        Guid offerId,
        CancellationToken cancellationToken = default)
        => SendWithoutBodyAsync(
            HttpMethod.Post,
            $"api/company-portal/{companyId:D}/mission-offers/{offerId:D}/refuse",
            token,
            null,
            cancellationToken);

    public Task<ApiCallResult<CompanyMissionChatResponse>> GetMissionMessagesAsync(
        string token,
        Guid companyId,
        Guid missionId,
        CancellationToken cancellationToken = default)
        => SendAsync<CompanyMissionChatResponse>(
            HttpMethod.Get,
            $"api/company-portal/{companyId:D}/missions/{missionId:D}/messages",
            token,
            null,
            cancellationToken);

    public Task<ApiCallResult<SendCompanyMissionMessageResponse>> SendMissionMessageAsync(
        string token,
        Guid companyId,
        Guid missionId,
        SendCompanyMissionMessageRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<SendCompanyMissionMessageResponse>(
            HttpMethod.Post,
            $"api/company-portal/{companyId:D}/missions/{missionId:D}/messages",
            token,
            request,
            cancellationToken);

    public Task<ApiCallResult<IReadOnlyList<CompanyEmployeeResponse>>> GetProvidersAsync(
        string token,
        Guid companyId,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<CompanyEmployeeResponse>>(
            HttpMethod.Get,
            $"api/company-portal/{companyId:D}/employees",
            token,
            null,
            cancellationToken);

    public Task<ApiCallResult<IReadOnlyList<CompanyInterimCandidateResponse>>> GetInterimCandidatesAsync(
        string token,
        Guid companyId,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<CompanyInterimCandidateResponse>>(
            HttpMethod.Get,
            $"api/company-portal/{companyId:D}/interim-candidates",
            token,
            null,
            cancellationToken);

    public Task<ApiCallResult<bool>> ApproveInterimCandidateAsync(
        string token,
        Guid companyId,
        Guid requestId,
        CompanyReviewInterimCandidateRequest request,
        CancellationToken cancellationToken = default)
        => SendWithoutBodyAsync(
            HttpMethod.Post,
            $"api/company-portal/{companyId:D}/interim-candidates/{requestId:D}/approve",
            token,
            request,
            cancellationToken);

    public Task<ApiCallResult<bool>> RejectInterimCandidateAsync(
        string token,
        Guid companyId,
        Guid requestId,
        CompanyReviewInterimCandidateRequest request,
        CancellationToken cancellationToken = default)
        => SendWithoutBodyAsync(
            HttpMethod.Post,
            $"api/company-portal/{companyId:D}/interim-candidates/{requestId:D}/reject",
            token,
            request,
            cancellationToken);

    public Task<ApiCallResult<bool>> ApproveEmployeeAsync(
        string token,
        Guid companyId,
        Guid employeeId,
        CancellationToken cancellationToken = default)
        => SendWithoutBodyAsync(
            HttpMethod.Post,
            $"api/company-portal/{companyId:D}/employees/{employeeId:D}/approve",
            token,
            null,
            cancellationToken);

    public Task<ApiCallResult<bool>> DeactivateEmployeeAsync(
        string token,
        Guid companyId,
        Guid employeeId,
        CancellationToken cancellationToken = default)
        => SendWithoutBodyAsync(
            HttpMethod.Delete,
            $"api/company-portal/{companyId:D}/employees/{employeeId:D}",
            token,
            null,
            cancellationToken);

    public Task<ApiCallResult<IReadOnlyList<CompanyPortalAssignableProviderResponse>>> GetAssignableProvidersAsync(
        string token,
        Guid companyId,
        Guid missionId,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<CompanyPortalAssignableProviderResponse>>(
            HttpMethod.Get,
            $"api/company-portal/{companyId:D}/missions/{missionId:D}/assignable-providers",
            token,
            null,
            cancellationToken);

    public Task<ApiCallResult<bool>> AssignMissionAsync(
        string token,
        Guid companyId,
        Guid missionId,
        AssignCompanyMissionRequest request,
        CancellationToken cancellationToken = default)
        => SendWithoutBodyAsync(
            HttpMethod.Post,
            $"api/company-portal/{companyId:D}/missions/{missionId:D}/assign",
            token,
            request,
            cancellationToken);

    public Task<ApiCallResult<IReadOnlyList<MissionAdditionalQuoteResponse>>> GetAdditionalQuotesAsync(
        string token,
        Guid companyId,
        Guid missionId,
        CancellationToken cancellationToken = default)
        => SendAsync<IReadOnlyList<MissionAdditionalQuoteResponse>>(
            HttpMethod.Get,
            $"api/company-portal/{companyId:D}/missions/{missionId:D}/additional-quotes",
            token,
            null,
            cancellationToken);

    public Task<ApiCallResult<bool>> SubmitAdditionalQuoteAsync(
        string token,
        Guid companyId,
        Guid missionId,
        Guid quoteId,
        SubmitMissionAdditionalQuoteRequest request,
        CancellationToken cancellationToken = default)
        => SendWithoutBodyAsync(
            HttpMethod.Post,
            $"api/company-portal/{companyId:D}/missions/{missionId:D}/additional-quotes/{quoteId:D}/submit",
            token,
            request,
            cancellationToken);

    public Task<ApiCallResult<CompanyPortalNotificationListResponse>> GetNotificationsAsync(
        string token,
        Guid companyId,
        CancellationToken cancellationToken = default)
        => SendAsync<CompanyPortalNotificationListResponse>(
            HttpMethod.Get,
            $"api/company-portal/{companyId:D}/notifications",
            token,
            null,
            cancellationToken);

    public Task<ApiCallResult<bool>> MarkNotificationReadAsync(
        string token,
        Guid companyId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
        => SendWithoutBodyAsync(
            HttpMethod.Post,
            $"api/company-portal/{companyId:D}/notifications/{notificationId:D}/mark-read",
            token,
            null,
            cancellationToken);

    public Task<ApiCallResult<MobileDeviceTokenResponse>> RegisterDeviceTokenAsync(
        string token,
        Guid companyId,
        RegisterMobileDeviceTokenRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<MobileDeviceTokenResponse>(
            HttpMethod.Post,
            $"api/company-portal/{companyId:D}/mobile/device-token",
            token,
            request,
            cancellationToken);

    private async Task<ApiCallResult<T>> SendAsync<T>(
        HttpMethod method,
        string path,
        string? bearerToken,
        object? body,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = BuildRequest(method, path, bearerToken, body);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiCallResult<T>.Failed(await ReadErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return payload is null
                ? ApiCallResult<T>.Failed("Réponse serveur vide.")
                : ApiCallResult<T>.Ok(payload);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiCallResult<T>.Failed("Le serveur met trop de temps à répondre.");
        }
        catch (Exception)
        {
            return ApiCallResult<T>.Failed("Connexion au service wélé impossible.");
        }
    }

    private async Task<ApiCallResult<bool>> SendWithoutBodyAsync(
        HttpMethod method,
        string path,
        string bearerToken,
        object? body,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = BuildRequest(method, path, bearerToken, body);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? ApiCallResult<bool>.Ok(true)
                : ApiCallResult<bool>.Failed(await ReadErrorAsync(response, cancellationToken));
        }
        catch (Exception)
        {
            return ApiCallResult<bool>.Failed("Connexion au service wélé impossible.");
        }
    }

    private static HttpRequestMessage BuildRequest(HttpMethod method, string path, string? bearerToken, object? body)
    {
        var request = new HttpRequestMessage(method, path);
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        return request;
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.TryGetProperty("message", out var message))
                {
                    return message.GetString() ?? "Action impossible.";
                }
            }
            catch (JsonException)
            {
                // Le statut HTTP reste la source de repli.
            }
        }

        return response.StatusCode == System.Net.HttpStatusCode.Unauthorized
            ? "Email ou mot de passe incorrect."
            : "Action impossible pour le moment.";
    }
}
