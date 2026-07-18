using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using HomeService.Contracts.Admin;
using HomeService.Contracts.Branding;
using HomeService.Contracts.Cms;
using HomeService.Contracts.Companies;
using HomeService.Contracts.Localization;
using HomeService.Contracts.Monitoring;
using HomeService.Contracts.Notifications;
using HomeService.Contracts.Services;
using Microsoft.AspNetCore.Components.Forms;

namespace HomeService.Admin.Services;

public sealed class PlatformApiClient(HttpClient httpClient, IConfiguration configuration)
{
    public Uri? BaseAddress => httpClient.BaseAddress;

    public async Task<IReadOnlyList<CompanyApplicationSummaryResponse>> GetCompanyApplicationsAsync(CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<IReadOnlyList<CompanyApplicationSummaryResponse>>("/api/admin/company-applications", cancellationToken) ?? [];
    }

    public async Task<AdminCompanyListResponse?> GetCompaniesAsync(
        string? status,
        string? search,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();

        var query = new List<string>();
        AddQueryValue(query, "status", status);
        AddQueryValue(query, "search", search);

        var suffix = query.Count == 0 ? string.Empty : $"?{string.Join('&', query)}";
        return await GetJsonAsync<AdminCompanyListResponse>($"/api/admin/companies{suffix}", cancellationToken);
    }

    public async Task<AdminCompanyDetailResponse?> GetCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<AdminCompanyDetailResponse>($"/api/admin/companies/{companyId}", cancellationToken);
    }

    public async Task<AdminMissionListResponse?> GetAdminMissionsAsync(
        string? status,
        string? search,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();

        var query = new List<string>();
        AddQueryValue(query, "status", status);
        AddQueryValue(query, "search", search);

        var suffix = query.Count == 0 ? string.Empty : $"?{string.Join('&', query)}";
        return await GetJsonAsync<AdminMissionListResponse>($"/api/admin/missions{suffix}", cancellationToken);
    }

    public async Task<AdminMissionListResponse?> MarkAdminMissionDisputedAsync(
        Guid missionId,
        string note,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<AdminMissionListResponse>(
            $"/api/admin/missions/{missionId}/mark-disputed",
            new AdminMissionActionRequest(note),
            cancellationToken);
    }

    public async Task<AdminProviderListResponse?> GetAdminProvidersAsync(
        string? status,
        string? employmentType,
        string? search,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();

        var query = new List<string>();
        AddQueryValue(query, "status", status);
        AddQueryValue(query, "employmentType", employmentType);
        AddQueryValue(query, "search", search);

        var suffix = query.Count == 0 ? string.Empty : $"?{string.Join('&', query)}";
        return await GetJsonAsync<AdminProviderListResponse>($"/api/admin/providers{suffix}", cancellationToken);
    }

    public async Task<AdminPaymentListResponse?> GetAdminPaymentsAsync(
        string? period,
        string? paymentStatus,
        string? paymentMethod,
        string? search,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();

        var query = new List<string>();
        AddQueryValue(query, "period", period);
        AddQueryValue(query, "paymentStatus", paymentStatus);
        AddQueryValue(query, "paymentMethod", paymentMethod);
        AddQueryValue(query, "search", search);

        var suffix = query.Count == 0 ? string.Empty : $"?{string.Join('&', query)}";
        return await GetJsonAsync<AdminPaymentListResponse>($"/api/admin/payments{suffix}", cancellationToken);
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

    public async Task<ServiceSummaryResponse?> CreateServiceAsync(
        UpsertServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<ServiceSummaryResponse>("/api/admin/services", request, cancellationToken);
    }

    public async Task<ServiceSummaryResponse?> UpdateServiceAsync(
        Guid serviceId,
        UpsertServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PutJsonAsync<ServiceSummaryResponse>($"/api/admin/services/{serviceId}", request, cancellationToken);
    }

    public async Task<ServiceSummaryResponse?> ActivateServiceAsync(Guid serviceId, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<ServiceSummaryResponse>($"/api/admin/services/{serviceId}/activate", null, cancellationToken);
    }

    public async Task<ServiceSummaryResponse?> DeactivateServiceAsync(Guid serviceId, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<ServiceSummaryResponse>($"/api/admin/services/{serviceId}/deactivate", null, cancellationToken);
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

    public async Task<AdminTranslationListResponse?> GetAdminTranslationsAsync(
        string? scope,
        string? search,
        string? language,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();

        var query = new List<string>();
        AddQueryValue(query, "scope", scope);
        AddQueryValue(query, "search", search);
        AddQueryValue(query, "language", language);

        var suffix = query.Count == 0 ? string.Empty : $"?{string.Join('&', query)}";
        return await GetJsonAsync<AdminTranslationListResponse>($"/api/admin/translations{suffix}", cancellationToken);
    }

    public async Task<AdminTranslationListResponse?> UpsertAdminTranslationAsync(
        UpsertAdminTranslationRequest request,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<AdminTranslationListResponse>("/api/admin/translations", request, cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationOutboxMessageResponse>> GetNotificationsAsync(CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<IReadOnlyList<NotificationOutboxMessageResponse>>("/api/admin/notifications", cancellationToken) ?? [];
    }

    public async Task<NotificationOutboxMessageResponse?> RetryNotificationAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<NotificationOutboxMessageResponse>($"/api/admin/notifications/{notificationId}/retry", null, cancellationToken);
    }

    public async Task<NotificationOutboxMessageResponse?> CancelNotificationAsync(Guid notificationId, string? reason, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<NotificationOutboxMessageResponse>(
            $"/api/admin/notifications/{notificationId}/cancel",
            new NotificationActionRequest(reason),
            cancellationToken);
    }

    public async Task<NotificationOutboxMessageResponse?> MarkNotificationSentAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<NotificationOutboxMessageResponse>($"/api/admin/notifications/{notificationId}/mark-sent", null, cancellationToken);
    }

    public async Task<AuditLogListResponse?> GetAuditLogsAsync(
        string? actorType,
        string? entityType,
        string? search,
        int skip,
        int take,
        string? contextType = null,
        Guid? contextId = null,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();

        var query = new List<string>
        {
            $"skip={skip}",
            $"take={take}"
        };

        AddQueryValue(query, "actorType", actorType);
        AddQueryValue(query, "entityType", entityType);
        AddQueryValue(query, "search", search);
        AddQueryValue(query, "contextType", contextType);
        if (contextId.HasValue)
        {
            AddQueryValue(query, "contextId", contextId.Value.ToString());
        }

        return await GetJsonAsync<AuditLogListResponse>($"/api/admin/audit-logs?{string.Join('&', query)}", cancellationToken);
    }

    public async Task<AdminAccessSnapshotResponse?> GetAdminAccessSnapshotAsync(CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<AdminAccessSnapshotResponse>("/api/admin/access-control", cancellationToken);
    }

    public async Task<AdminAccessSnapshotResponse?> CreateAdminRoleAsync(CreateAdminRoleRequest request, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<AdminAccessSnapshotResponse>("/api/admin/access-control/roles", request, cancellationToken);
    }

    public async Task<AdminAccessSnapshotResponse?> UpdateAdminRolePermissionsAsync(
        Guid roleId,
        UpdateAdminRolePermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PutJsonAsync<AdminAccessSnapshotResponse>($"/api/admin/access-control/roles/{roleId}/permissions", request, cancellationToken);
    }

    public async Task<AdminAccessSnapshotResponse?> CreateAdminUserAsync(CreateAdminUserRequest request, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<AdminAccessSnapshotResponse>("/api/admin/access-control/admins", request, cancellationToken);
    }

    public async Task<AdminAccessSnapshotResponse?> UpdateAdminUserRolesAsync(
        Guid adminUserId,
        UpdateAdminUserRolesRequest request,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PutJsonAsync<AdminAccessSnapshotResponse>($"/api/admin/access-control/admins/{adminUserId}/roles", request, cancellationToken);
    }

    public async Task<AdminAccessSnapshotResponse?> DeactivateAdminUserAsync(Guid adminUserId, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<AdminAccessSnapshotResponse>($"/api/admin/access-control/admins/{adminUserId}/deactivate", null, cancellationToken);
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

    public async Task<CmsPageDetailResponse?> GetCmsPageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<CmsPageDetailResponse>($"/api/admin/cms/pages/{id}", cancellationToken);
    }

    public async Task<CmsContentValueResponse?> UpdateCmsContentValueAsync(
        Guid id,
        UpdateCmsContentValueRequest request,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PutJsonAsync<CmsContentValueResponse>($"/api/admin/cms/content-values/{id}", request, cancellationToken);
    }

    public async Task<CmsMediaUploadResponse?> UploadCmsMediaAsync(
        Guid contentValueId,
        IBrowserFile file,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        using var content = new MultipartFormDataContent();
        await using var sourceStream = file.OpenReadStream(8 * 1024 * 1024, cancellationToken);
        using var memoryStream = new MemoryStream();
        await sourceStream.CopyToAsync(memoryStream, cancellationToken);

        var fileContent = new ByteArrayContent(memoryStream.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
        content.Add(fileContent, "file", file.Name);

        using var response = await httpClient.PostAsync($"/api/admin/cms/content-values/{contentValueId}/media", content, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<CmsMediaUploadResponse>(cancellationToken);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new PlatformApiException(
            $"API {(int)response.StatusCode} {response.ReasonPhrase} sur {new Uri(httpClient.BaseAddress!, $"/api/admin/cms/content-values/{contentValueId}/media")}. {body}");
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

    public string GetProviderDocumentPreviewUrl(Guid documentId)
    {
        return ToApiUrl($"/api/admin/provider-documents/{documentId}/preview");
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

    private static void AddQueryValue(List<string> query, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value.Trim())}");
        }
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
