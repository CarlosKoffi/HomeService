using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using HomeService.Contracts.Companies;
using HomeService.Contracts.Localization;
using HomeService.Contracts.Services;

namespace HomeService.Admin.Services;

public sealed class PlatformApiClient(HttpClient httpClient, IConfiguration configuration)
{
    public Uri? BaseAddress => httpClient.BaseAddress;

    public async Task<IReadOnlyList<CompanyApplicationSummaryResponse>> GetCompanyApplicationsAsync(CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<IReadOnlyList<CompanyApplicationSummaryResponse>>("/api/admin/company-applications", cancellationToken) ?? [];
    }

    public async Task<CompanyApplicationDetailResponse?> GetCompanyApplicationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<CompanyApplicationDetailResponse>($"/api/admin/company-applications/{id}", cancellationToken);
    }

    public async Task<IReadOnlyList<ServiceSummaryResponse>> GetServicesAsync(CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<IReadOnlyList<ServiceSummaryResponse>>("/api/services", cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<TranslationValueResponse>> GetTranslationsAsync(string scope, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<IReadOnlyList<TranslationValueResponse>>($"/api/translations?scope={Uri.EscapeDataString(scope)}", cancellationToken) ?? [];
    }

    public string GetCompanyApplicationDocumentUrl(Guid documentId)
    {
        return new Uri(httpClient.BaseAddress!, $"/api/admin/company-application-documents/{documentId}/download").ToString();
    }

    private async Task<T?> GetJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(path, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new PlatformApiException(
            $"API {(int)response.StatusCode} {response.ReasonPhrase} sur {new Uri(httpClient.BaseAddress!, path)}. {body}");
    }

    private void AddBasicAuthIfConfigured()
    {
        if (!IsAuthEnabled())
        {
            return;
        }

        var password = configuration["SITE_AUTH_PASSWORD"]?.Trim();
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var username = (configuration["SITE_AUTH_USERNAME"] ?? "admin").Trim();
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
    }

    private bool IsAuthEnabled()
    {
        var value = configuration["SITE_AUTH_ENABLED"];
        return !string.Equals(value?.Trim(), "false", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(value?.Trim(), "0", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class PlatformApiException(string message) : Exception(message);
