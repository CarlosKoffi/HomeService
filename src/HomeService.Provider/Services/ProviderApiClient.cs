using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HomeService.Contracts.Cms;
using HomeService.Contracts.Contact;
using HomeService.Contracts.ProviderPortal;
using HomeService.Contracts.Services;

namespace HomeService.Provider.Services;

public sealed class ProviderApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ApiResult<ProviderInvitationPreviewResponse>> GetInvitationAsync(string code, CancellationToken cancellationToken = default)
    {
        return await SendAsync<ProviderInvitationPreviewResponse>(
            () => httpClient.GetAsync($"/api/provider-portal/invitations/{Uri.EscapeDataString(code)}", cancellationToken),
            cancellationToken);
    }

    public async Task<IReadOnlyList<ServiceSummaryResponse>> GetServicesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<IReadOnlyList<ServiceSummaryResponse>>("/api/services", JsonOptions, cancellationToken) ?? [];
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<ProviderOnboardingOptionResponse>> GetOnboardingOptionsAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = string.IsNullOrWhiteSpace(search)
                ? "/api/provider-onboarding/options"
                : $"/api/provider-onboarding/options?search={Uri.EscapeDataString(search)}";
            return await httpClient.GetFromJsonAsync<IReadOnlyList<ProviderOnboardingOptionResponse>>(
                url,
                JsonOptions,
                cancellationToken) ?? [];
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<ProviderCompanyOpportunityResponse>> GetOnboardingOpportunitiesAsync(
        string selectionType,
        Guid selectionId,
        string? address,
        CancellationToken cancellationToken = default)
    {
        if (selectionId == Guid.Empty)
        {
            return [];
        }

        try
        {
            var url = $"/api/provider-onboarding/opportunities?selectionType={Uri.EscapeDataString(selectionType)}&selectionId={selectionId}";
            if (!string.IsNullOrWhiteSpace(address))
            {
                url += $"&address={Uri.EscapeDataString(address)}";
            }

            return await httpClient.GetFromJsonAsync<IReadOnlyList<ProviderCompanyOpportunityResponse>>(
                url,
                JsonOptions,
                cancellationToken) ?? [];
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return [];
        }
    }

    public string ToApiUrl(string? relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(relativeUrl, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.ToString();
        }

        return new Uri(httpClient.BaseAddress!, relativeUrl.TrimStart('/')).ToString();
    }

    public async Task<CompanyHomeCmsResponse?> GetProviderHomeContentAsync(
        string language = "fr",
        string country = "CI",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<CompanyHomeCmsResponse>(
                $"/api/cms/provider/home?language={Uri.EscapeDataString(language)}&country={Uri.EscapeDataString(country)}",
                JsonOptions,
                cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return null;
        }
    }

    public async Task<ApiResult<SubmitContactResponse>> SubmitContactRequestAsync(
        SubmitContactRequest request,
        CancellationToken cancellationToken = default)
    {
        return await SendAsync<SubmitContactResponse>(
            () => httpClient.PostAsJsonAsync("/api/contact-requests", request, JsonOptions, cancellationToken),
            cancellationToken);
    }

    public async Task<ApiResult<ProviderPortalLoginResponse>> ActivateAsync(
        ProviderInvitationActivationRequest request,
        CancellationToken cancellationToken = default)
    {
        return await SendAsync<ProviderPortalLoginResponse>(
            () => httpClient.PostAsJsonAsync("/api/provider-portal/activate", request, JsonOptions, cancellationToken),
            cancellationToken);
    }

    public async Task<ApiResult<ProviderPortalLoginResponse>> LoginAsync(
        ProviderPortalLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        return await SendAsync<ProviderPortalLoginResponse>(
            () => httpClient.PostAsJsonAsync("/api/provider-portal/login", request, JsonOptions, cancellationToken),
            cancellationToken);
    }

    public async Task<ApiResult<ProviderSelfRegistrationResponse>> RegisterSelfAsync(
        ProviderSelfRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        return await SendAsync<ProviderSelfRegistrationResponse>(
            () => httpClient.PostAsJsonAsync("/api/provider-onboarding/self-registration", request, JsonOptions, cancellationToken),
            cancellationToken);
    }

    public async Task<ApiResult<ProviderMobileHomeResponse>> GetHomeAsync(string token, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/provider-portal/mobile/home");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await SendAsync<ProviderMobileHomeResponse>(
            () => httpClient.SendAsync(request, cancellationToken),
            cancellationToken);
    }

    private static async Task<ApiResult<T>> SendAsync<T>(
        Func<Task<HttpResponseMessage>> send,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await send();
            if (!response.IsSuccessStatusCode)
            {
                var message = await ExtractMessageAsync(response, cancellationToken);
                return ApiResult<T>.Failure(message ?? $"API {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return payload is null
                ? ApiResult<T>.Failure("Reponse API vide.")
                : ApiResult<T>.Success(payload);
        }
        catch (Exception exception)
        {
            return ApiResult<T>.Failure(exception.Message);
        }
    }

    private static async Task<string?> ExtractMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
            if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("message", out var message))
            {
                return message.GetString();
            }
        }
        catch
        {
            return null;
        }

        return null;
    }
}

public sealed record ApiResult<T>(bool IsSuccess, T? Value, string? ErrorMessage)
{
    public static ApiResult<T> Success(T value) => new(true, value, null);

    public static ApiResult<T> Failure(string message) => new(false, default, message);
}
