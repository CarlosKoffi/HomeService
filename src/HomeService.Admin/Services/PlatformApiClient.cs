using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using HomeService.Contracts.Admin;
using HomeService.Contracts.Branding;
using HomeService.Contracts.Cms;
using PaymentProviderResponse = HomeService.Contracts.Clients.PaymentProviderResponse;
using UpsertPaymentProviderRequest = HomeService.Contracts.Clients.UpsertPaymentProviderRequest;
using HomeService.Contracts.Companies;
using HomeService.Contracts.Contact;
using HomeService.Contracts.Localization;
using HomeService.Contracts.Monitoring;
using HomeService.Contracts.Missions;
using HomeService.Contracts.Notifications;
using HomeService.Contracts.Services;
using Microsoft.AspNetCore.Components.Forms;

namespace HomeService.Admin.Services;

public sealed class PlatformApiClient(HttpClient httpClient, IConfiguration configuration, AdminApiSessionAccessor adminSessionAccessor)
{
    public Uri? BaseAddress => httpClient.BaseAddress;

    public async Task<AdminLoginResponse?> LoginAdminAsync(AdminLoginRequest request, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<AdminLoginResponse>("/api/admin/auth/login", request, cancellationToken);
    }

    public async Task<AdminCurrentUserResponse?> GetCurrentAdminAsync(CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<AdminCurrentUserResponse>("/api/admin/auth/me", cancellationToken);
    }

    public async Task LogoutAdminAsync(CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        using var response = await httpClient.PostAsync("/api/admin/auth/logout", null, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw CreateApiException(response, "/api/admin/auth/logout", body);
        }
    }

    public async Task<AdminDashboardResponse?> GetAdminDashboardAsync(CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<AdminDashboardResponse>("/api/admin/dashboard", cancellationToken);
    }

    public async Task<AdminQualityDashboardResponse?> GetAdminQualityDashboardAsync(CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<AdminQualityDashboardResponse>("/api/admin/quality", cancellationToken);
    }

    public async Task<AdminProviderQualificationResponse?> ReviewQualityQualificationAsync(
        Guid id, ReviewAdminProviderQualificationRequest request, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PutJsonAsync<AdminProviderQualificationResponse>($"/api/admin/quality/qualifications/{id:D}", request, cancellationToken);
    }

    public async Task<IReadOnlyList<AdminProviderQualificationResponse>> GetQualityQualificationsAsync(
        string? status = null, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var suffix = string.IsNullOrWhiteSpace(status) ? string.Empty : $"?status={Uri.EscapeDataString(status)}";
        return await GetJsonAsync<IReadOnlyList<AdminProviderQualificationResponse>>($"/api/admin/quality/qualifications{suffix}", cancellationToken) ?? [];
    }

    public async Task<AdminQualityAuditResponse?> ReviewQualityAuditAsync(
        Guid id, ReviewAdminQualityAuditRequest request, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PutJsonAsync<AdminQualityAuditResponse>($"/api/admin/quality/audits/{id:D}", request, cancellationToken);
    }

    public async Task<AdminQualityChecklistTemplateResponse?> UpdateQualityTemplateAsync(
        Guid id, UpdateAdminQualityChecklistTemplateRequest request, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PutJsonAsync<AdminQualityChecklistTemplateResponse>($"/api/admin/quality/templates/{id:D}", request, cancellationToken);
    }

    public async Task<IReadOnlyList<CompanyApplicationSummaryResponse>> GetCompanyApplicationsAsync(CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<IReadOnlyList<CompanyApplicationSummaryResponse>>("/api/admin/company-applications", cancellationToken) ?? [];
    }

    public async Task<AdminCompanyListResponse?> GetCompaniesAsync(
        string? status,
        string? search,
        string? service,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();

        var query = new List<string>();
        AddQueryValue(query, "status", status);
        AddQueryValue(query, "search", search);
        AddQueryValue(query, "service", service);

        var suffix = query.Count == 0 ? string.Empty : $"?{string.Join('&', query)}";
        return await GetJsonAsync<AdminCompanyListResponse>($"/api/admin/companies{suffix}", cancellationToken);
    }

    public async Task<AdminCompanyDetailResponse?> GetCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<AdminCompanyDetailResponse>($"/api/admin/companies/{companyId}", cancellationToken);
    }

    public async Task<AdminClientListResponse?> GetAdminClientsAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var suffix = string.IsNullOrWhiteSpace(search)
            ? string.Empty
            : $"?search={Uri.EscapeDataString(search.Trim())}";
        return await GetJsonAsync<AdminClientListResponse>($"/api/admin/clients{suffix}", cancellationToken);
    }

    public async Task<AdminClientDetailResponse?> GetAdminClientAsync(
        Guid clientId,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<AdminClientDetailResponse>($"/api/admin/clients/{clientId:D}", cancellationToken);
    }

    public string GetAdminClientAttachmentPreviewUrl(Guid attachmentId)
    {
        return $"/admin-client-attachments/{attachmentId:D}/preview";
    }

    public async Task<ApiActionResult> SuspendAdminCompanyAsync(
        Guid companyId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var response = await httpClient.PostAsJsonAsync(
            $"/api/admin/companies/{companyId}/suspend",
            new AdminCompanyActionRequest(note),
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return response.IsSuccessStatusCode
            ? new ApiActionResult(true, null)
            : new ApiActionResult(false, ExtractErrorMessage(body) ?? response.ReasonPhrase ?? "Suspension impossible.");
    }

    public async Task<ApiActionResult> ReactivateAdminCompanyAsync(
        Guid companyId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var response = await httpClient.PostAsJsonAsync(
            $"/api/admin/companies/{companyId}/reactivate",
            new AdminCompanyActionRequest(note),
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return response.IsSuccessStatusCode
            ? new ApiActionResult(true, null)
            : new ApiActionResult(false, ExtractErrorMessage(body) ?? response.ReasonPhrase ?? "Reactivation impossible.");
    }

    public async Task<ApiActionResult> UpdateCompanyDispatchSettingsAsync(
        Guid companyId,
        int missionDispatchPriority,
        bool acceptsUrgentMissions,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var response = await httpClient.PutAsJsonAsync(
            $"/api/admin/companies/{companyId}/dispatch-settings",
            new UpdateAdminCompanyDispatchSettingsRequest(missionDispatchPriority, acceptsUrgentMissions),
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return response.IsSuccessStatusCode
            ? new ApiActionResult(true, null)
            : new ApiActionResult(false, ExtractErrorMessage(body) ?? response.ReasonPhrase ?? "Mise a jour impossible.");
    }

    public async Task<ApiActionResult> MarkCompanyNotificationReadAsync(
        Guid companyId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var response = await httpClient.PostAsync(
            $"/api/admin/companies/{companyId:D}/notifications/{notificationId:D}/mark-read",
            null,
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return response.IsSuccessStatusCode
            ? new ApiActionResult(true, null)
            : new ApiActionResult(false, ExtractErrorMessage(body) ?? response.ReasonPhrase ?? "Action impossible.");
    }

    public async Task<ApiActionResult> MarkCompanyNotificationUnreadAsync(
        Guid companyId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var response = await httpClient.PostAsync(
            $"/api/admin/companies/{companyId:D}/notifications/{notificationId:D}/mark-unread",
            null,
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return response.IsSuccessStatusCode
            ? new ApiActionResult(true, null)
            : new ApiActionResult(false, ExtractErrorMessage(body) ?? response.ReasonPhrase ?? "Action impossible.");
    }

    public async Task<ApiActionResult> ResendCompanyNotificationAsync(
        Guid companyId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var response = await httpClient.PostAsync(
            $"/api/admin/companies/{companyId:D}/notifications/{notificationId:D}/resend",
            null,
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return response.IsSuccessStatusCode
            ? new ApiActionResult(true, null)
            : new ApiActionResult(false, ExtractErrorMessage(body) ?? response.ReasonPhrase ?? "Action impossible.");
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

    public async Task<AdminMissionDetailResponse?> GetAdminMissionAsync(Guid missionId, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<AdminMissionDetailResponse>($"/api/admin/missions/{missionId}", cancellationToken);
    }

    public async Task<IReadOnlyList<AdminMissionDispatchOfferResponse>> CreateAdminMissionDispatchOffersAsync(
        Guid missionId,
        bool urgent,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var suffix = urgent ? "?urgent=true" : "?urgent=false";
        return await PostJsonAsync<IReadOnlyList<AdminMissionDispatchOfferResponse>>(
            $"/api/admin/missions/{missionId}/dispatch-offers{suffix}",
            null,
            cancellationToken) ?? [];
    }

    public async Task<AdminMissionListResponse?> MarkAdminMissionDisputedAsync(
        Guid missionId,
        string reason,
        string note,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<AdminMissionListResponse>(
            $"/api/admin/missions/{missionId}/mark-disputed",
            new OpenMissionDisputeRequest(reason, note),
            cancellationToken);
    }

    public async Task<AdminMissionDetailResponse?> ResolveAdminMissionDisputeAsync(
        Guid missionId,
        string resolution,
        string note,
        int? refundPercent,
        int? refundAmount,
        bool includeCustomerServiceFeeInRefund = false,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<AdminMissionDetailResponse>(
            $"/api/admin/missions/{missionId}/resolve-dispute",
            new ResolveMissionDisputeRequest(
                resolution,
                note,
                refundPercent,
                refundAmount,
                includeCustomerServiceFeeInRefund),
            cancellationToken);
    }

    public async Task<AdminMissionDetailResponse?> CancelAdminMissionAsync(
        Guid missionId,
        string reason,
        string note,
        int? cancellationFeeAmount,
        int? refundPercent = null,
        bool includeCustomerServiceFeeInRefund = false,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<AdminMissionDetailResponse>(
            $"/api/admin/missions/{missionId}/cancel",
            new CancelMissionRequest(
                reason,
                note,
                cancellationFeeAmount,
                refundPercent,
                includeCustomerServiceFeeInRefund),
            cancellationToken);
    }

    public async Task<AdminMissionSettingsResponse?> GetAdminMissionSettingsAsync(CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<AdminMissionSettingsResponse>("/api/admin/mission-settings", cancellationToken);
    }

    public async Task<AdminMissionSettingsResponse?> UpdateAdminCommissionRuleAsync(
        Guid ruleId,
        UpdateAdminCommissionRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PutJsonAsync<AdminMissionSettingsResponse>(
            $"/api/admin/mission-settings/commission-rules/{ruleId:D}",
            request,
            cancellationToken);
    }

    public async Task<AdminMissionSettingsResponse?> UpdateAdminMissionWorkflowSettingAsync(
        Guid settingId,
        UpdateAdminMissionWorkflowSettingRequest request,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PutJsonAsync<AdminMissionSettingsResponse>(
            $"/api/admin/mission-settings/workflow-settings/{settingId:D}",
            request,
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

    public async Task<AdminProviderDetailResponse?> GetAdminProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<AdminProviderDetailResponse>($"/api/admin/providers/{providerId}", cancellationToken);
    }

    public async Task<ApiActionResult> ApproveAdminProviderAsync(
        Guid providerId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var response = await httpClient.PostAsJsonAsync(
            $"/api/admin/providers/{providerId}/approve",
            new AdminProviderActionRequest(note),
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return response.IsSuccessStatusCode
            ? new ApiActionResult(true, null)
            : new ApiActionResult(false, ExtractErrorMessage(body) ?? response.ReasonPhrase ?? "Validation impossible.");
    }

    public async Task<ApiActionResult> SuspendAdminProviderAsync(
        Guid providerId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var response = await httpClient.PostAsJsonAsync(
            $"/api/admin/providers/{providerId}/suspend",
            new AdminProviderActionRequest(note),
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return response.IsSuccessStatusCode
            ? new ApiActionResult(true, null)
            : new ApiActionResult(false, ExtractErrorMessage(body) ?? response.ReasonPhrase ?? "Suspension impossible.");
    }

    public async Task<ApiActionResult> SetAdminProviderAvailabilityAsync(
        Guid providerId,
        bool isAvailable,
        string? note,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var response = await httpClient.PutAsJsonAsync(
            $"/api/admin/providers/{providerId}/availability",
            new AdminProviderAvailabilityRequest(isAvailable, note),
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return response.IsSuccessStatusCode
            ? new ApiActionResult(true, null)
            : new ApiActionResult(false, ExtractErrorMessage(body) ?? response.ReasonPhrase ?? "Changement de disponibilite impossible.");
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

    public async Task<IReadOnlyList<AdminCompanyPayoutResponse>> GetAdminCompanyPayoutsAsync(
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<IReadOnlyList<AdminCompanyPayoutResponse>>(
            "/api/admin/company-payouts",
            cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<AdminCompanyPayoutDestinationResponse>> GetAdminCompanyPayoutDestinationsAsync(
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<IReadOnlyList<AdminCompanyPayoutDestinationResponse>>(
            "/api/admin/company-payout-destinations",
            cancellationToken) ?? [];
    }

    public async Task<ApiActionResult> VerifyCompanyPayoutDestinationAsync(
        Guid destinationId,
        string? externalContactId = null,
        CancellationToken cancellationToken = default) =>
        await PostAdminPayoutActionAsync(
            $"/api/admin/company-payout-destinations/{destinationId:D}/verify",
            new VerifyCompanyPayoutDestinationRequest(externalContactId),
            cancellationToken);

    public async Task<ApiActionResult> ApproveCompanyPayoutAsync(
        Guid payoutId,
        CancellationToken cancellationToken = default) =>
        await PostAdminPayoutActionAsync(
            $"/api/admin/company-payouts/{payoutId:D}/approve",
            new ReviewCompanyPayoutRequest(),
            cancellationToken);

    public async Task<ApiActionResult> RejectCompanyPayoutAsync(
        Guid payoutId,
        string? reason,
        CancellationToken cancellationToken = default) =>
        await PostAdminPayoutActionAsync(
            $"/api/admin/company-payouts/{payoutId:D}/reject",
            new ReviewCompanyPayoutRequest(reason),
            cancellationToken);

    public async Task<ApiActionResult> CompleteCashCompanyPayoutAsync(
        Guid payoutId,
        string proofReference,
        CancellationToken cancellationToken = default) =>
        await PostAdminPayoutActionAsync(
            $"/api/admin/company-payouts/{payoutId:D}/complete-cash",
            new ReviewCompanyPayoutRequest(ProofReference: proofReference),
            cancellationToken);

    private async Task<ApiActionResult> PostAdminPayoutActionAsync(
        string path,
        object payload,
        CancellationToken cancellationToken)
    {
        AddBasicAuthIfConfigured();
        using var response = await httpClient.PostAsJsonAsync(path, payload, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return response.IsSuccessStatusCode
            ? new ApiActionResult(true, null)
            : new ApiActionResult(false, ExtractErrorMessage(body) ?? response.ReasonPhrase ?? "Action de reversement impossible.");
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

    public async Task<IReadOnlyList<ServiceSummaryResponse>> GetAdminServicesAsync(CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<IReadOnlyList<ServiceSummaryResponse>>("/api/admin/services", cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<PaymentProviderResponse>> GetPaymentProvidersAsync(CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<IReadOnlyList<PaymentProviderResponse>>("/api/admin/payment-providers", cancellationToken) ?? [];
    }

    public async Task<PaymentProviderResponse?> CreatePaymentProviderAsync(UpsertPaymentProviderRequest request, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<PaymentProviderResponse>("/api/admin/payment-providers", request, cancellationToken);
    }

    public async Task<PaymentProviderResponse?> UpdatePaymentProviderAsync(Guid id, UpsertPaymentProviderRequest request, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PutJsonAsync<PaymentProviderResponse>($"/api/admin/payment-providers/{id}", request, cancellationToken);
    }

    public async Task<CompanyServiceProposalListResponse?> GetCompanyServiceProposalsAsync(CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<CompanyServiceProposalListResponse>("/api/admin/company-service-proposals", cancellationToken);
    }

    public async Task<ServiceCatalogInsightListResponse?> GetServiceCatalogInsightsAsync(CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<ServiceCatalogInsightListResponse>("/api/admin/service-insights", cancellationToken);
    }

    public async Task<CompanyServiceProposalListResponse?> ReanalyseCompanyServiceProposalsAsync(CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<CompanyServiceProposalListResponse>("/api/admin/company-service-proposals/reanalyse", null, cancellationToken);
    }

    public async Task<CompanyServiceProposalListResponse?> AttachCompanyServiceProposalAsync(
        Guid proposalId,
        AttachCompanyServiceProposalRequest request,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<CompanyServiceProposalListResponse>(
            $"/api/admin/company-service-proposals/{proposalId}/attach",
            request,
            cancellationToken);
    }

    public async Task<CompanyServiceProposalListResponse?> CreatePrestationFromCompanyServiceProposalAsync(
        Guid proposalId,
        CreatePrestationFromCompanyServiceProposalRequest request,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<CompanyServiceProposalListResponse>(
            $"/api/admin/company-service-proposals/{proposalId}/create-prestation",
            request,
            cancellationToken);
    }

    public async Task<CompanyServiceProposalListResponse?> CreateServiceFromCompanyServiceProposalAsync(
        Guid proposalId,
        CreateServiceFromCompanyServiceProposalRequest request,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<CompanyServiceProposalListResponse>(
            $"/api/admin/company-service-proposals/{proposalId}/create-service",
            request,
            cancellationToken);
    }

    public async Task<CompanyServiceProposalListResponse?> RejectCompanyServiceProposalAsync(
        Guid proposalId,
        RejectCompanyServiceProposalRequest request,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<CompanyServiceProposalListResponse>(
            $"/api/admin/company-service-proposals/{proposalId}/reject",
            request,
            cancellationToken);
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

    public async Task DeleteServiceAsync(Guid serviceId, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var path = $"/api/admin/services/{serviceId}";
        using var response = await httpClient.DeleteAsync(path, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw CreateApiException(response, path, body);
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

    public async Task<ServiceOptionSummaryResponse?> CreateServiceOptionAsync(
        Guid prestationId,
        UpsertServiceOptionRequest request,
        CancellationToken cancellationToken = default)
    {
        return await PostJsonAsync<ServiceOptionSummaryResponse>(
            $"/api/admin/service-prestations/{prestationId}/options",
            request,
            cancellationToken);
    }

    public async Task<ServiceOptionSummaryResponse?> UpdateServiceOptionAsync(
        Guid optionId,
        UpsertServiceOptionRequest request,
        CancellationToken cancellationToken = default)
    {
        return await PutJsonAsync<ServiceOptionSummaryResponse>(
            $"/api/admin/service-options/{optionId}",
            request,
            cancellationToken);
    }

    public async Task<ServiceOptionSummaryResponse?> SetServiceOptionActiveAsync(
        Guid optionId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var state = isActive ? "activate" : "deactivate";
        return await PostJsonAsync<ServiceOptionSummaryResponse>(
            $"/api/admin/service-options/{optionId}/{state}",
            null,
            cancellationToken);
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

    public async Task<IReadOnlyList<AdminContactRequestResponse>> GetContactRequestsAsync(
        string? status,
        string? source,
        string? search,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();

        var query = new List<string>();
        AddQueryValue(query, "status", status);
        AddQueryValue(query, "source", source);
        AddQueryValue(query, "search", search);

        var suffix = query.Count == 0 ? string.Empty : $"?{string.Join('&', query)}";
        return await GetJsonAsync<IReadOnlyList<AdminContactRequestResponse>>($"/api/admin/contact-requests{suffix}", cancellationToken) ?? [];
    }

    public async Task<AdminContactRequestResponse?> MarkContactRequestInProgressAsync(
        Guid contactRequestId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<AdminContactRequestResponse>(
            $"/api/admin/contact-requests/{contactRequestId:D}/in-progress",
            new UpdateContactRequestStatusRequest(note),
            cancellationToken);
    }

    public async Task<AdminContactRequestResponse?> CloseContactRequestAsync(
        Guid contactRequestId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<AdminContactRequestResponse>(
            $"/api/admin/contact-requests/{contactRequestId:D}/close",
            new UpdateContactRequestStatusRequest(note),
            cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationOutboxMessageResponse>> GetNotificationsAsync(CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<IReadOnlyList<NotificationOutboxMessageResponse>>("/api/admin/notifications", cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<AdminCompanyPortalNotificationResponse>> GetCompanyPortalNotificationsAsync(CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<IReadOnlyList<AdminCompanyPortalNotificationResponse>>("/api/admin/company-portal-notifications", cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<NotificationDeliveryRuleResponse>> GetNotificationDeliveryRulesAsync(CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<IReadOnlyList<NotificationDeliveryRuleResponse>>("/api/admin/notification-delivery-rules", cancellationToken) ?? [];
    }

    public async Task<NotificationDeliveryRuleResponse?> UpdateNotificationDeliveryRuleAsync(
        Guid ruleId,
        UpdateNotificationDeliveryRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PutJsonAsync<NotificationDeliveryRuleResponse>(
            $"/api/admin/notification-delivery-rules/{ruleId:D}",
            request,
            cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationTemplateResponse>> GetNotificationTemplatesAsync(CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<IReadOnlyList<NotificationTemplateResponse>>("/api/admin/notification-templates", cancellationToken) ?? [];
    }

    public async Task<NotificationTemplateResponse?> CreateNotificationTemplateAsync(
        CreateNotificationTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<NotificationTemplateResponse>(
            "/api/admin/notification-templates",
            request,
            cancellationToken);
    }

    public async Task<NotificationTemplateResponse?> UpdateNotificationTemplateAsync(
        Guid templateId,
        UpdateNotificationTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PutJsonAsync<NotificationTemplateResponse>(
            $"/api/admin/notification-templates/{templateId:D}",
            request,
            cancellationToken);
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

    public async Task<AdminInvitationResponse?> CreateAdminInvitationAsync(CreateAdminUserRequest request, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<AdminInvitationResponse>("/api/admin/access-control/admins/invitations", request, cancellationToken);
    }

    public async Task<AdminInvitationDetailResponse?> GetAdminInvitationAsync(string token, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await GetJsonAsync<AdminInvitationDetailResponse>($"/api/admin/access-control/admins/invitations/{Uri.EscapeDataString(token)}", cancellationToken);
    }

    public async Task<AdminAccessSnapshotResponse?> AcceptAdminInvitationAsync(
        string token,
        AcceptAdminInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<AdminAccessSnapshotResponse>($"/api/admin/access-control/admins/invitations/{Uri.EscapeDataString(token)}/password", request, cancellationToken);
    }

    public async Task<AdminAccessSnapshotResponse?> UpdateAdminUserProfileAsync(
        Guid adminUserId,
        UpdateAdminUserProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PutJsonAsync<AdminAccessSnapshotResponse>($"/api/admin/access-control/admins/{adminUserId}/profile", request, cancellationToken);
    }

    public async Task<AdminInvitationResponse?> RegenerateAdminInvitationAsync(Guid adminUserId, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<AdminInvitationResponse>($"/api/admin/access-control/admins/{adminUserId}/invitation", null, cancellationToken);
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

    public async Task<AdminAccessSnapshotResponse?> ReactivateAdminUserAsync(Guid adminUserId, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await PostJsonAsync<AdminAccessSnapshotResponse>($"/api/admin/access-control/admins/{adminUserId}/reactivate", null, cancellationToken);
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
        throw CreateApiException(response, $"/api/admin/cms/content-values/{contentValueId}/media", body);
    }

    public async Task<CmsMediaUploadResponse?> UploadCmsMediaAsync(
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

        const string path = "/api/admin/cms/media";
        using var response = await httpClient.PostAsync(path, content, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<CmsMediaUploadResponse>(cancellationToken);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw CreateApiException(response, path, body);
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

    public string ToCmsMediaPreviewUrl(string? mediaUrl, string? surface)
    {
        if (string.IsNullOrWhiteSpace(mediaUrl))
        {
            return string.Empty;
        }

        var value = mediaUrl.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteUri))
        {
            var localCmsMediaProxy = TryBuildLocalCmsMediaProxyUrl(absoluteUri.AbsolutePath);
            if (!string.IsNullOrWhiteSpace(localCmsMediaProxy))
            {
                return localCmsMediaProxy;
            }

            var localPublicMediaProxy = TryBuildLocalPublicMediaProxyUrl(absoluteUri.AbsolutePath);
            if (!string.IsNullOrWhiteSpace(localPublicMediaProxy))
            {
                return localPublicMediaProxy;
            }

            var localPublicAssetProxy = TryBuildLocalPublicAssetProxyUrl(absoluteUri.AbsolutePath);
            if (!string.IsNullOrWhiteSpace(localPublicAssetProxy))
            {
                return localPublicAssetProxy;
            }

            return absoluteUri.ToString();
        }

        var cmsMediaProxy = TryBuildLocalCmsMediaProxyUrl(value);
        if (!string.IsNullOrWhiteSpace(cmsMediaProxy))
        {
            return cmsMediaProxy;
        }

        var publicMediaProxy = TryBuildLocalPublicMediaProxyUrl(value);
        if (!string.IsNullOrWhiteSpace(publicMediaProxy))
        {
            return publicMediaProxy;
        }

        var publicAssetProxy = TryBuildLocalPublicAssetProxyUrl(value);
        if (!string.IsNullOrWhiteSpace(publicAssetProxy))
        {
            return publicAssetProxy;
        }

        if (value.StartsWith("images/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/images/", StringComparison.OrdinalIgnoreCase))
        {
            return "/" + value.TrimStart('/');
        }

        if (value.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("api/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/storage/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("storage/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/media/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("media/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return ToPublicApiUrl(value);
        }

        var publicBaseUrl = ResolvePublicBaseUrl(surface);
        return new Uri(publicBaseUrl ?? httpClient.BaseAddress!, value.TrimStart('/')).ToString();
    }

    public async Task<CompanyApplicationDocumentFile> GetCmsMediaFileAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var path = $"/api/cms/media/{mediaId}";
        using var response = await httpClient.GetAsync(path, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw CreateApiException(response, path, body);
        }

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName
            ?? $"cms-media-{mediaId}";

        return new CompanyApplicationDocumentFile(content, contentType, fileName.Trim('"'));
    }

    public async Task<CompanyApplicationDocumentFile> GetPublicMediaFileAsync(
        string mediaPath,
        CancellationToken cancellationToken = default)
    {
        var normalizedPath = mediaPath.Trim().TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalizedPath)
            || normalizedPath.Contains("..", StringComparison.Ordinal)
            || !normalizedPath.StartsWith("media/", StringComparison.OrdinalIgnoreCase))
        {
            throw new PlatformApiException("Chemin media invalide.");
        }

        AddBasicAuthIfConfigured();
        var path = "/" + normalizedPath;
        using var response = await httpClient.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw CreateApiException(response, path, body);
        }

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        return new CompanyApplicationDocumentFile(content, contentType, Path.GetFileName(normalizedPath));
    }

    public async Task<CompanyApplicationDocumentFile> GetPublicAssetFileAsync(
        string assetPath,
        CancellationToken cancellationToken = default)
    {
        var normalizedPath = assetPath.Trim().TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalizedPath)
            || normalizedPath.Contains("..", StringComparison.Ordinal)
            || !normalizedPath.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
        {
            throw new PlatformApiException("Chemin d'asset invalide.");
        }

        AddBasicAuthIfConfigured();
        var path = "/" + normalizedPath;
        using var response = await httpClient.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw CreateApiException(response, path, body);
        }

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        return new CompanyApplicationDocumentFile(content, contentType, Path.GetFileName(normalizedPath));
    }

    private static string? TryBuildLocalCmsMediaProxyUrl(string path)
    {
        var normalized = path.Trim();
        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            normalized = "/" + normalized;
        }

        const string cmsMediaPrefix = "/api/cms/media/";
        if (!normalized.StartsWith(cmsMediaPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var idSegment = normalized[cmsMediaPrefix.Length..].Split('/', '?', '#')[0];
        return Guid.TryParse(idSegment, out var mediaId)
            ? $"/admin-cms-media/{mediaId}/preview"
            : null;
    }

    private static string? TryBuildLocalPublicMediaProxyUrl(string path)
    {
        var normalized = path.Trim();
        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            normalized = "/" + normalized;
        }

        if (!normalized.StartsWith("/media/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var mediaPath = normalized.TrimStart('/').Split('?', '#')[0];
        return string.IsNullOrWhiteSpace(mediaPath) ? null : "/admin-api-media/" + mediaPath;
    }

    private static string? TryBuildLocalPublicAssetProxyUrl(string path)
    {
        var normalized = path.Trim();
        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            normalized = "/" + normalized;
        }

        if (!normalized.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var assetPath = normalized.TrimStart('/').Split('?', '#')[0];
        return string.IsNullOrWhiteSpace(assetPath) ? null : "/admin-api-assets/" + assetPath;
    }

    private Uri? ResolvePublicBaseUrl(string? surface)
    {
        var key = surface switch
        {
            "PublicCompany" => configuration["COMPANY_PUBLIC_BASE_URL"] ?? configuration["CompanyPublicBaseUrl"],
            "PublicProvider" => configuration["PROVIDER_PUBLIC_BASE_URL"] ?? configuration["ProviderPublicBaseUrl"],
            "PublicClient" => configuration["CLIENT_PUBLIC_BASE_URL"] ?? configuration["ClientPublicBaseUrl"],
            _ => null
        };

        return string.IsNullOrWhiteSpace(key)
            ? null
            : new Uri(key.TrimEnd('/') + "/");
    }

    private string ToPublicApiUrl(string relativeUrl)
    {
        var publicApiBaseUrl = configuration["API_PUBLIC_BASE_URL"] ?? configuration["ApiPublicBaseUrl"];
        if (string.IsNullOrWhiteSpace(publicApiBaseUrl))
        {
            return ToApiUrl(relativeUrl);
        }

        return new Uri(new Uri(publicApiBaseUrl.TrimEnd('/') + "/"), relativeUrl.TrimStart('/')).ToString();
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
        return $"/admin-provider-documents/{documentId}/preview";
    }

    public async Task<CompanyApplicationDocumentFile> GetCompanyApplicationDocumentFileAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var path = $"/api/admin/company-application-documents/{documentId}/download";
        using var response = await httpClient.GetAsync(path, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw CreateApiException(response, path, body);
        }

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName
            ?? "document";

        return new CompanyApplicationDocumentFile(content, contentType, fileName.Trim('"'));
    }

    public async Task<CompanyApplicationDocumentFile> GetProviderDocumentFileAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var path = $"/api/admin/provider-documents/{documentId}/preview";
        using var response = await httpClient.GetAsync(path, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw CreateApiException(response, path, body);
        }

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName
            ?? "document-prestataire";

        return new CompanyApplicationDocumentFile(content, contentType, fileName.Trim('"'));
    }

    public async Task<CompanyApplicationDocumentFile> GetAdminClientAttachmentFileAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var path = $"/api/admin/client-attachments/{attachmentId:D}/preview";
        using var response = await httpClient.GetAsync(path, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw CreateApiException(response, path, body);
        }

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName
            ?? "piece-client";

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
        throw CreateApiException(response, path, body);
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
            ? await httpClient.PostAsJsonAsync(path, new { }, cancellationToken)
            : await httpClient.PostAsJsonAsync(path, payload, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw CreateApiException(response, path, body);
    }

    private async Task<T?> PutJsonAsync<T>(string path, object payload, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PutAsJsonAsync(path, payload, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw CreateApiException(response, path, body);
    }

    private void AddBasicAuthIfConfigured()
    {
        httpClient.DefaultRequestHeaders.Remove("X-Admin-Session");
        if (!string.IsNullOrWhiteSpace(adminSessionAccessor.Token))
        {
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Admin-Session", adminSessionAccessor.Token);
        }

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

    private PlatformApiException CreateApiException(HttpResponseMessage response, string path, string body)
    {
        var details = ExtractErrorMessage(body);
        var url = new Uri(httpClient.BaseAddress!, path);
        var prefix = $"API {(int)response.StatusCode} {response.ReasonPhrase} sur {url}.";
        return string.IsNullOrWhiteSpace(details)
            ? new PlatformApiException(prefix)
            : new PlatformApiException($"{prefix} {details}");
    }

    private static string? ExtractErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("message", out var message))
            {
                return message.GetString();
            }

            if (document.RootElement.TryGetProperty("detail", out var detail))
            {
                return detail.GetString();
            }
        }
        catch (System.Text.Json.JsonException)
        {
            return body;
        }

        return body;
    }
}

public sealed class PlatformApiException(string message) : Exception(message);

public sealed record ApiActionResult(bool IsSuccess, string? ErrorMessage);

public sealed record CompanyApplicationDocumentFile(byte[] Content, string ContentType, string FileName);
