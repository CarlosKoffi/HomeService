using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using HomeService.Contracts.Companies;
using HomeService.Contracts.Localization;
using HomeService.Contracts.Services;

namespace HomeService.Admin.Services;

public sealed class PlatformApiClient(HttpClient httpClient, IConfiguration configuration)
{
    public async Task<IReadOnlyList<CompanyApplicationSummaryResponse>> GetCompanyApplicationsAsync(CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await httpClient.GetFromJsonAsync<IReadOnlyList<CompanyApplicationSummaryResponse>>("/api/admin/company-applications", cancellationToken) ?? [];
    }

    public async Task<CompanyApplicationDetailResponse?> GetCompanyApplicationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await httpClient.GetFromJsonAsync<CompanyApplicationDetailResponse>($"/api/admin/company-applications/{id}", cancellationToken);
    }

    public async Task<IReadOnlyList<ServiceSummaryResponse>> GetServicesAsync(CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await httpClient.GetFromJsonAsync<IReadOnlyList<ServiceSummaryResponse>>("/api/services", cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<TranslationValueResponse>> GetTranslationsAsync(string scope, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await httpClient.GetFromJsonAsync<IReadOnlyList<TranslationValueResponse>>($"/api/translations?scope={Uri.EscapeDataString(scope)}", cancellationToken) ?? [];
    }

    private void AddBasicAuthIfConfigured()
    {
        var password = configuration["SITE_AUTH_PASSWORD"];
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var username = configuration["SITE_AUTH_USERNAME"] ?? "admin";
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
    }
}
