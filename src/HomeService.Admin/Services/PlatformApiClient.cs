using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using HomeService.Contracts.Branding;
using HomeService.Contracts.Cms;
using HomeService.Contracts.Companies;
using HomeService.Contracts.Localization;
using HomeService.Contracts.Notifications;
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

    public async Task<CompanyApplicationActionResponse?> ApproveCompanyApplicationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<CompanyApplicationActionResponse>($"/api/admin/company-applications/{id}/approve", null, cancellationToken);
    }

    public async Task<CompanyApplicationActionResponse?> RejectCompanyApplicationAsync(Guid id, string note, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<CompanyApplicationActionResponse>($"/api/admin/company-applications/{id}/reject", new CompanyApplicationReviewRequest(note), cancellationToken);
    }

    public async Task<CompanyApplicationActionResponse?> ReopenCompanyApplicationAsync(Guid id, string note, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<CompanyApplicationActionResponse>($"/api/admin/company-applications/{id}/reopen", new CompanyApplicationReviewRequest(note), cancellationToken);
    }

    public async Task<CompanyApplicationActionResponse?> RequestCompanyApplicationMoreInformationAsync(Guid id, string note, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<CompanyApplicationActionResponse>($"/api/admin/company-applications/{id}/request-more-information", new CompanyApplicationReviewRequest(note), cancellationToken);
    }

    public async Task<CompanyApplicationActivationLinkResponse?> SendCompanyApplicationActivationLinkAsync(Guid id, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<CompanyApplicationActivationLinkResponse>($"/api/admin/company-applications/{id}/activation-link", null, cancellationToken);
    }

    public async Task<CompanyApplicationDocumentReviewResponse?> ApproveCompanyApplicationDocumentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<CompanyApplicationDocumentReviewResponse>($"/api/admin/company-application-documents/{id}/approve", null, cancellationToken);
    }

    public async Task<CompanyApplicationDocumentReviewResponse?> RejectCompanyApplicationDocumentAsync(Guid id, string comment, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<CompanyApplicationDocumentReviewResponse>($"/api/admin/company-application-documents/{id}/reject", new CompanyApplicationDocumentReviewRequest(comment), cancellationToken);
    }

    public async Task<CompanyApplicationDocumentReviewResponse?> RequestCompanyApplicationDocumentReplacementAsync(Guid id, string comment, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<CompanyApplicationDocumentReviewResponse>($"/api/admin/company-application-documents/{id}/request-replacement", new CompanyApplicationDocumentReviewRequest(comment), cancellationToken);
    }

    public async Task<CompanyApplicationDocumentReviewResponse?> ReopenCompanyApplicationDocumentAsync(Guid id, string comment, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<CompanyApplicationDocumentReviewResponse>($"/api/admin/company-application-documents/{id}/reopen", new CompanyApplicationDocumentReviewRequest(comment), cancellationToken);
    }

    public async Task<IReadOnlyList<ServiceSummaryResponse>> GetServicesAsync(CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<IReadOnlyList<ServiceSummaryResponse>>("/api/services", cancellationToken) ?? [];
    }

    public async Task<ServicePrestationSummaryResponse?> CreateServicePrestationAsync(
        Guid serviceId,
        UpsertServicePrestationRequest request,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<ServicePrestationSummaryResponse>(
            $"/api/admin/services/{serviceId}/prestations",
            request,
            cancellationToken);
    }

    public async Task<ServicePrestationSummaryResponse?> UpdateServicePrestationAsync(
        Guid prestationId,
        UpsertServicePrestationRequest request,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PutJsonAsync<ServicePrestationSummaryResponse>(
            $"/api/admin/service-prestations/{prestationId}",
            request,
            cancellationToken);
    }

    public async Task<ServicePrestationSummaryResponse?> ActivateServicePrestationAsync(Guid prestationId, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<ServicePrestationSummaryResponse>($"/api/admin/service-prestations/{prestationId}/activate", null, cancellationToken);
    }

    public async Task<ServicePrestationSummaryResponse?> DeactivateServicePrestationAsync(Guid prestationId, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<ServicePrestationSummaryResponse>($"/api/admin/service-prestations/{prestationId}/deactivate", null, cancellationToken);
    }

    public async Task<IReadOnlyList<TranslationValueResponse>> GetTranslationsAsync(string scope, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<IReadOnlyList<TranslationValueResponse>>($"/api/translations?scope={Uri.EscapeDataString(scope)}", cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<NotificationOutboxMessageResponse>> GetNotificationsAsync(CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<IReadOnlyList<NotificationOutboxMessageResponse>>("/api/admin/notifications", cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<CmsSiteSummaryResponse>> GetCmsSitesAsync(CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<IReadOnlyList<CmsSiteSummaryResponse>>("/api/admin/cms/sites", cancellationToken) ?? [];
    }

    public async Task<CmsSiteDetailResponse?> GetCmsSiteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<CmsSiteDetailResponse>($"/api/admin/cms/sites/{id}", cancellationToken);
    }

    public async Task<IReadOnlyList<CmsComponentDefinitionResponse>> GetCmsComponentDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<IReadOnlyList<CmsComponentDefinitionResponse>>("/api/admin/cms/component-definitions", cancellationToken) ?? [];
    }

    public async Task<CountryBrandingResponse?> GetCountryBrandingAsync(string countryCode, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<CountryBrandingResponse>($"/api/admin/country-brandings/{Uri.EscapeDataString(countryCode)}", cancellationToken);
    }

    public async Task<CountryBrandingResponse?> UpdateCountryBrandingAsync(string countryCode, UpdateCountryBrandingRequest request, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PutJsonAsync<CountryBrandingResponse>($"/api/admin/country-brandings/{Uri.EscapeDataString(countryCode)}", request, cancellationToken);
    }

    public async Task<CompanyAssignmentModeResponse?> UpdateCompanyAssignmentModeAsync(Guid companyId, string assignmentMode, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PutJsonAsync<CompanyAssignmentModeResponse>(
            $"/api/admin/companies/{companyId}/assignment-mode",
            new UpdateCompanyAssignmentModeRequest(assignmentMode),
            cancellationToken);
    }

    public string GetCompanyApplicationDocumentUrl(Guid documentId)
    {
        return new Uri(httpClient.BaseAddress!, $"/api/admin/company-application-documents/{documentId}/download").ToString();
    }

    public string GetCompanyApplicationDocumentPreviewUrl(Guid documentId)
    {
        return $"/admin-documents/{documentId}/preview";
    }

    public async Task<CompanyApplicationDocumentFile> GetCompanyApplicationDocumentFileAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var path = $"/api/admin/company-application-documents/{documentId}/download";
        using var response = await httpClient.GetAsync(path, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new PlatformApiException(
                $"API {(int)response.StatusCode} {response.ReasonPhrase} sur {new Uri(httpClient.BaseAddress!, path)}. {body}");
        }

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName
            ?? "document";

        return new CompanyApplicationDocumentFile(content, contentType, fileName.Trim('"'));
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

    private async Task<T?> PostJsonAsync<T>(string path, object? payload, CancellationToken cancellationToken)
    {
        using var response = payload is null
            ? await httpClient.PostAsync(path, null, cancellationToken)
            : await httpClient.PostAsJsonAsync(path, payload, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new PlatformApiException(
            $"API {(int)response.StatusCode} {response.ReasonPhrase} sur {new Uri(httpClient.BaseAddress!, path)}. {body}");
    }

    private async Task<T?> PutJsonAsync<T>(string path, object payload, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PutAsJsonAsync(path, payload, cancellationToken);

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

public sealed record CompanyApplicationDocumentFile(byte[] Content, string ContentType, string FileName);
