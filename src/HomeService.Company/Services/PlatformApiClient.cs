using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HomeService.Contracts.Branding;
using HomeService.Contracts.Cms;
using HomeService.Contracts.Companies;
using HomeService.Contracts.CompanyPortal;
using HomeService.Contracts.Localization;
using HomeService.Contracts.Services;
using Microsoft.AspNetCore.Components.Forms;

namespace HomeService.Company.Services;

public sealed class PlatformApiClient(HttpClient httpClient, IConfiguration configuration)
{
    private const long MaxUploadSize = 10 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

    public async Task<IReadOnlyList<ServiceSummaryResponse>> GetServicesAsync(CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await httpClient.GetFromJsonAsync<IReadOnlyList<ServiceSummaryResponse>>("/api/services", cancellationToken) ?? [];
    }

    public async Task<CountryBrandingResponse?> GetCountryBrandingAsync(string countryCode = "CI", CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await httpClient.GetFromJsonAsync<CountryBrandingResponse>($"/api/country-branding?country={Uri.EscapeDataString(countryCode)}", cancellationToken);
    }

    public async Task<CompanyHomeCmsResponse?> GetCompanyHomeContentAsync(
        string language = "fr",
        string country = "CI",
        CancellationToken cancellationToken = default)
    {
        try
        {
            AddBasicAuthIfConfigured();
            return await httpClient.GetFromJsonAsync<CompanyHomeCmsResponse>(
                $"/api/cms/company/home?language={Uri.EscapeDataString(language)}&country={Uri.EscapeDataString(country)}",
                JsonOptions,
                cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return null;
        }
    }

    public async Task<RegisterCompanyResult> RegisterCompanyAsync(
        RegisterCompanyRequest request,
        IReadOnlyDictionary<string, IBrowserFile?>? documents = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            AddBasicAuthIfConfigured();
            using var content = new MultipartFormDataContent();
            AddString(content, "companyName", request.CompanyName);
            AddString(content, "registrationNumber", request.RegistrationNumber);
            AddString(content, "city", request.City);
            AddString(content, "address", request.Address);
            AddString(content, "contactName", request.ContactName);
            AddString(content, "email", request.Email);
            AddString(content, "phoneNumber", request.PhoneNumber);
            AddString(content, "password", request.Password);
            AddString(content, "confirmPassword", request.ConfirmPassword);
            AddString(content, "estimatedProviderCount", request.EstimatedProviderCount?.ToString());

            foreach (var service in request.Services)
            {
                AddString(content, "services", service);
            }

            foreach (var (fieldName, file) in documents ?? new Dictionary<string, IBrowserFile?>())
            {
                if (file is null)
                {
                    continue;
                }

                await using var sourceStream = file.OpenReadStream(MaxUploadSize, cancellationToken);
                using var memoryStream = new MemoryStream();
                await sourceStream.CopyToAsync(memoryStream, cancellationToken);

                var fileContent = new ByteArrayContent(memoryStream.ToArray());
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                    string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
                content.Add(fileContent, fieldName, file.Name);
            }

            var response = await httpClient.PostAsync("/api/company-applications", content, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return new RegisterCompanyResult(true, null);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return new RegisterCompanyResult(false, ExtractErrorMessage(body) ?? response.ReasonPhrase ?? "Erreur API inconnue.");
        }
        catch (TaskCanceledException)
        {
            return new RegisterCompanyResult(false, "L'envoi a pris trop de temps. Verifiez la connexion et relancez la demande.");
        }
        catch (IOException)
        {
            return new RegisterCompanyResult(false, "Une piece jointe n'a pas pu etre lue correctement. Retirez-la puis ajoutez-la a nouveau.");
        }
        catch (InvalidOperationException exception)
        {
            return new RegisterCompanyResult(false, exception.Message);
        }
    }

    public async Task<IReadOnlyList<TranslationValueResponse>> GetTranslationsAsync(string scope, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await httpClient.GetFromJsonAsync<IReadOnlyList<TranslationValueResponse>>($"/api/translations?scope={Uri.EscapeDataString(scope)}", cancellationToken) ?? [];
    }

    public async Task<CompanyActivationResult> CreateCompanyPasswordAsync(
        CompanyActivationPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var response = await httpClient.PostAsJsonAsync("/api/company-activation/password", request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadFromJsonAsync<CompanyActivationPasswordResponse>(cancellationToken);
            return new CompanyActivationResult(true, payload?.Message ?? "Mot de passe cree.");
        }

        return new CompanyActivationResult(false, ExtractErrorMessage(body) ?? response.ReasonPhrase ?? "Erreur API inconnue.");
    }

    public async Task<CompanyActivationPreviewResult> GetCompanyActivationPreviewAsync(
        Guid applicationId,
        string token,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var response = await httpClient.GetAsync($"/api/company-activation/{applicationId}?token={Uri.EscapeDataString(token)}", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadFromJsonAsync<CompanyActivationPreviewResponse>(cancellationToken);
            return payload is null
                ? new CompanyActivationPreviewResult(false, null, "Lien d'activation introuvable.")
                : new CompanyActivationPreviewResult(true, payload, null);
        }

        return new CompanyActivationPreviewResult(false, null, ExtractErrorMessage(body) ?? "Lien d'activation invalide ou expire.");
    }

    public async Task<CompanyPortalLoginResult> LoginCompanyPortalAsync(
        CompanyPortalLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var response = await httpClient.PostAsJsonAsync("/api/company-portal/login", request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadFromJsonAsync<CompanyPortalLoginResponse>(cancellationToken);
            return payload is null
                ? new CompanyPortalLoginResult(false, null, "Connexion impossible pour le moment.")
                : new CompanyPortalLoginResult(true, new CompanyPortalSessionResponse(
                    payload.CompanyId,
                    payload.CompanyName,
                    payload.UserName,
                    payload.Email,
                    DateTimeOffset.UtcNow,
                    payload.Token,
                    payload.CompanyStatus,
                    payload.IsCompanyApproved), null);
        }

        return new CompanyPortalLoginResult(false, null, ExtractErrorMessage(body) ?? "Identifiants incorrects ou entreprise inactive.");
    }

    public async Task<RegisterCompanyResult> UploadCompanyComplianceDocumentsAsync(
        Guid companyId,
        IReadOnlyDictionary<string, IBrowserFile?> documents,
        CancellationToken cancellationToken = default)
    {
        try
        {
            AddBasicAuthIfConfigured();
            using var content = new MultipartFormDataContent();
            foreach (var (fieldName, file) in documents)
            {
                if (file is null)
                {
                    continue;
                }

                await using var sourceStream = file.OpenReadStream(MaxUploadSize, cancellationToken);
                using var memoryStream = new MemoryStream();
                await sourceStream.CopyToAsync(memoryStream, cancellationToken);

                var fileContent = new ByteArrayContent(memoryStream.ToArray());
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                    string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
                content.Add(fileContent, fieldName, file.Name);
            }

            var response = await httpClient.PostAsync($"/api/company-portal/{companyId:D}/compliance-documents", content, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return new RegisterCompanyResult(true, null);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return new RegisterCompanyResult(false, ExtractErrorMessage(body) ?? response.ReasonPhrase ?? "Erreur API inconnue.");
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or TaskCanceledException)
        {
            return new RegisterCompanyResult(false, exception is TaskCanceledException
                ? "L'envoi a pris trop de temps. Verifiez la connexion puis relancez."
                : exception.Message);
        }
    }

    public async Task<CompanyPortalProfileResponse?> GetCompanyProfileAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await httpClient.GetFromJsonAsync<CompanyPortalProfileResponse>($"/api/company-portal/{companyId:D}/profile", cancellationToken);
    }

    public Task<EmployeeSaveResult> UpdateCompanyInformationAsync(
        Guid companyId,
        UpdateCompanyPortalCompanyInfoRequest request,
        CancellationToken cancellationToken = default)
    {
        return PutCompanyProfileAsync($"/api/company-portal/{companyId:D}/profile/company", request, "Modification des informations entreprise impossible.", cancellationToken);
    }

    public Task<EmployeeSaveResult> UpdateCompanyContactAsync(
        Guid companyId,
        UpdateCompanyPortalContactRequest request,
        CancellationToken cancellationToken = default)
    {
        return PutCompanyProfileAsync($"/api/company-portal/{companyId:D}/profile/contact", request, "Modification du responsable impossible.", cancellationToken);
    }

    public Task<EmployeeSaveResult> UpdateCompanyOperationsAsync(
        Guid companyId,
        UpdateCompanyPortalOperationsRequest request,
        CancellationToken cancellationToken = default)
    {
        return PutCompanyProfileAsync($"/api/company-portal/{companyId:D}/profile/operations", request, "Modification des zones et services impossible.", cancellationToken);
    }

    public Task<EmployeeSaveResult> UpdateCompanyPaymentAsync(
        Guid companyId,
        UpdateCompanyPortalPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        return PutCompanyProfileAsync($"/api/company-portal/{companyId:D}/profile/payment", request, "Modification du paiement impossible.", cancellationToken);
    }

    public async Task<IReadOnlyList<CompanyEmployeeResponse>> GetCompanyEmployeesAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await httpClient.GetFromJsonAsync<IReadOnlyList<CompanyEmployeeResponse>>($"/api/company-portal/{companyId:D}/employees", cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<CompanyInterimCandidateResponse>> GetInterimCandidatesAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await httpClient.GetFromJsonAsync<IReadOnlyList<CompanyInterimCandidateResponse>>(
            $"/api/company-portal/{companyId:D}/interim-candidates",
            cancellationToken) ?? [];
    }

    public async Task<CompanyPortalDashboardResponse?> GetCompanyDashboardAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await httpClient.GetFromJsonAsync<CompanyPortalDashboardResponse>(
            $"/api/company-portal/{companyId:D}/dashboard",
            cancellationToken);
    }

    public async Task<EmployeeSaveResult> ApproveInterimCandidateAsync(
        Guid companyId,
        Guid requestId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var response = await httpClient.PostAsJsonAsync(
            $"/api/company-portal/{companyId:D}/interim-candidates/{requestId:D}/approve",
            new CompanyReviewInterimCandidateRequest(note),
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return response.IsSuccessStatusCode
            ? new EmployeeSaveResult(true, null)
            : new EmployeeSaveResult(false, ExtractErrorMessage(body) ?? response.ReasonPhrase ?? "Validation impossible.");
    }

    public async Task<EmployeeSaveResult> RejectInterimCandidateAsync(
        Guid companyId,
        Guid requestId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var response = await httpClient.PostAsJsonAsync(
            $"/api/company-portal/{companyId:D}/interim-candidates/{requestId:D}/reject",
            new CompanyReviewInterimCandidateRequest(note),
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return response.IsSuccessStatusCode
            ? new EmployeeSaveResult(true, null)
            : new EmployeeSaveResult(false, ExtractErrorMessage(body) ?? response.ReasonPhrase ?? "Refus impossible.");
    }

    public async Task<EmployeeSaveResult> CreateCompanyEmployeeAsync(
        Guid companyId,
        CompanyEmployeeFormModel employee,
        CancellationToken cancellationToken = default)
    {
        try
        {
            AddBasicAuthIfConfigured();
            using var content = new MultipartFormDataContent();
            AddString(content, "firstName", employee.FirstName);
            AddString(content, "lastName", employee.LastName);
            AddString(content, "phoneNumber", employee.PhoneNumber);
            AddString(content, "email", employee.Email);
            AddString(content, "dateOfBirth", employee.DateOfBirth?.ToString("yyyy-MM-dd"));
            AddString(content, "address", employee.Address);
            AddString(content, "gender", employee.Gender);
            AddString(content, "employmentType", employee.EmploymentType);
            AddString(content, "yearsOfExperience", employee.YearsOfExperience.ToString());
            AddString(content, "missionLatitude", employee.MissionLatitude?.ToString());
            AddString(content, "missionLongitude", employee.MissionLongitude?.ToString());
            AddString(content, "missionRadiusKm", employee.MissionRadiusKm.ToString());
            AddString(content, "experienceLevel", employee.ExperienceLevel);

            foreach (var serviceId in employee.ServiceIds)
            {
                AddString(content, "serviceIds", serviceId.ToString("D"));
            }

            await AddFileAsync(content, "photo", employee.Photo, cancellationToken);
            await AddFileAsync(content, "identityDocument", employee.IdentityDocument, cancellationToken);
            await AddFileAsync(content, "diplomaDocument", employee.DiplomaDocument, cancellationToken);

            var response = await httpClient.PostAsync($"/api/company-portal/{companyId:D}/employees", content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            return response.IsSuccessStatusCode
                ? new EmployeeSaveResult(true, null)
                : new EmployeeSaveResult(false, ExtractErrorMessage(body) ?? response.ReasonPhrase ?? "Erreur API inconnue.");
        }
        catch (TaskCanceledException)
        {
            return new EmployeeSaveResult(false, "L'envoi a pris trop de temps. Verifiez la connexion puis relancez.");
        }
        catch (IOException)
        {
            return new EmployeeSaveResult(false, "Un fichier employe n'a pas pu etre lu correctement.");
        }
        catch (InvalidOperationException exception)
        {
            return new EmployeeSaveResult(false, exception.Message);
        }
    }

    public async Task<EmployeeInvitationCodeResult> GenerateCompanyEmployeeInvitationCodeAsync(
        Guid companyId,
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var response = await httpClient.PostAsync($"/api/company-portal/{companyId:D}/employees/{employeeId:D}/invitation-code", null, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new EmployeeInvitationCodeResult(false, null, ExtractErrorMessage(body) ?? response.ReasonPhrase ?? "Generation impossible.");
        }

        var invitation = JsonSerializer.Deserialize<CreateCompanyEmployeeResult>(body, JsonOptions);
        return new EmployeeInvitationCodeResult(true, invitation, null);
    }

    public async Task<EmployeeSaveResult> UpdateCompanyEmployeeAsync(
        Guid companyId,
        Guid employeeId,
        UpdateCompanyEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var response = await httpClient.PutAsJsonAsync($"/api/company-portal/{companyId:D}/employees/{employeeId:D}", request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return response.IsSuccessStatusCode
            ? new EmployeeSaveResult(true, null)
            : new EmployeeSaveResult(false, ExtractErrorMessage(body) ?? response.ReasonPhrase ?? "Modification impossible.");
    }

    public async Task<EmployeeSaveResult> UpdateCompanyEmployeeServicesAsync(
        Guid companyId,
        Guid employeeId,
        UpdateCompanyEmployeeServicesRequest request,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var response = await httpClient.PutAsJsonAsync($"/api/company-portal/{companyId:D}/employees/{employeeId:D}/services", request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return response.IsSuccessStatusCode
            ? new EmployeeSaveResult(true, null)
            : new EmployeeSaveResult(false, ExtractErrorMessage(body) ?? response.ReasonPhrase ?? "Modification des services impossible.");
    }

    public async Task<EmployeeSaveResult> UploadCompanyEmployeeDocumentAsync(
        Guid companyId,
        Guid employeeId,
        string documentType,
        IBrowserFile file,
        CancellationToken cancellationToken = default)
    {
        try
        {
            AddBasicAuthIfConfigured();
            using var content = new MultipartFormDataContent();
            AddString(content, "documentType", documentType);
            await AddFileAsync(content, "file", file, cancellationToken);

            var response = await httpClient.PostAsync($"/api/company-portal/{companyId:D}/employees/{employeeId:D}/documents", content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return response.IsSuccessStatusCode
                ? new EmployeeSaveResult(true, null)
                : new EmployeeSaveResult(false, ExtractErrorMessage(body) ?? response.ReasonPhrase ?? "Envoi de la piece impossible.");
        }
        catch (TaskCanceledException)
        {
            return new EmployeeSaveResult(false, "L'envoi a pris trop de temps. Verifiez la connexion puis relancez.");
        }
        catch (IOException)
        {
            return new EmployeeSaveResult(false, "Le fichier n'a pas pu etre lu correctement.");
        }
        catch (InvalidOperationException exception)
        {
            return new EmployeeSaveResult(false, exception.Message);
        }
    }

    public async Task<EmployeeSaveResult> DeleteCompanyEmployeeDocumentAsync(
        Guid companyId,
        Guid employeeId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var response = await httpClient.DeleteAsync($"/api/company-portal/{companyId:D}/employees/{employeeId:D}/documents/{documentId:D}", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return response.IsSuccessStatusCode
            ? new EmployeeSaveResult(true, null)
            : new EmployeeSaveResult(false, ExtractErrorMessage(body) ?? response.ReasonPhrase ?? "Suppression de la piece impossible.");
    }

    public async Task<EmployeeSaveResult> SuspendCompanyEmployeeAsync(Guid companyId, Guid employeeId, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var response = await httpClient.PostAsync($"/api/company-portal/{companyId:D}/employees/{employeeId:D}/suspend", null, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return response.IsSuccessStatusCode
            ? new EmployeeSaveResult(true, null)
            : new EmployeeSaveResult(false, ExtractErrorMessage(body) ?? response.ReasonPhrase ?? "Action impossible.");
    }

    public async Task<EmployeeSaveResult> DeleteCompanyEmployeeAsync(Guid companyId, Guid employeeId, CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var response = await httpClient.DeleteAsync($"/api/company-portal/{companyId:D}/employees/{employeeId:D}", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return response.IsSuccessStatusCode
            ? new EmployeeSaveResult(true, null)
            : new EmployeeSaveResult(false, ExtractErrorMessage(body) ?? response.ReasonPhrase ?? "Action impossible.");
    }

    public async Task<IReadOnlyList<CompanyPortalMissionResponse>> GetCompanyMissionsAsync(
        Guid companyId,
        string view,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await httpClient.GetFromJsonAsync<IReadOnlyList<CompanyPortalMissionResponse>>(
            $"/api/company-portal/{companyId:D}/missions?view={Uri.EscapeDataString(view)}",
            cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<CompanyPortalAssignableProviderResponse>> GetAssignableProvidersAsync(
        Guid companyId,
        Guid missionId,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await httpClient.GetFromJsonAsync<IReadOnlyList<CompanyPortalAssignableProviderResponse>>(
            $"/api/company-portal/{companyId:D}/missions/{missionId:D}/assignable-providers",
            cancellationToken) ?? [];
    }

    public async Task<EmployeeSaveResult> AssignCompanyMissionAsync(
        Guid companyId,
        Guid missionId,
        Guid providerId,
        int quotedAmount,
        string? overMaxJustification,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        var response = await httpClient.PostAsJsonAsync(
            $"/api/company-portal/{companyId:D}/missions/{missionId:D}/assign",
            new AssignCompanyMissionRequest(providerId, quotedAmount, overMaxJustification),
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return response.IsSuccessStatusCode
            ? new EmployeeSaveResult(true, null)
            : new EmployeeSaveResult(false, ExtractErrorMessage(body) ?? response.ReasonPhrase ?? "Affectation impossible.");
    }

    public async Task<CompanyPortalPaymentSummaryResponse?> GetCompanyPaymentsAsync(
        Guid companyId,
        string period,
        CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await httpClient.GetFromJsonAsync<CompanyPortalPaymentSummaryResponse>(
            $"/api/company-portal/{companyId:D}/payments?period={Uri.EscapeDataString(period)}",
            cancellationToken);
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

    private static void AddString(MultipartFormDataContent content, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            content.Add(new StringContent(value, Encoding.UTF8), name);
        }
    }

    private static async Task AddFileAsync(MultipartFormDataContent content, string name, IBrowserFile? file, CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return;
        }

        await using var sourceStream = file.OpenReadStream(MaxUploadSize, cancellationToken);
        using var memoryStream = new MemoryStream();
        await sourceStream.CopyToAsync(memoryStream, cancellationToken);

        var fileContent = new ByteArrayContent(memoryStream.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
        content.Add(fileContent, name, file.Name);
    }

    private bool IsAuthEnabled()
    {
        var value = configuration["SITE_AUTH_ENABLED"];
        return !string.Equals(value?.Trim(), "false", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(value?.Trim(), "0", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<EmployeeSaveResult> PutCompanyProfileAsync<TRequest>(
        string url,
        TRequest request,
        string fallbackMessage,
        CancellationToken cancellationToken)
    {
        AddBasicAuthIfConfigured();
        var response = await httpClient.PutAsJsonAsync(url, request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return response.IsSuccessStatusCode
            ? new EmployeeSaveResult(true, null)
            : new EmployeeSaveResult(false, ExtractErrorMessage(body) ?? response.ReasonPhrase ?? fallbackMessage);
    }

    private static string? ExtractErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
            {
                return string.Join(" ", errors.EnumerateArray().Select(error => error.GetString()).Where(error => !string.IsNullOrWhiteSpace(error)));
            }

            if (document.RootElement.TryGetProperty("message", out var message))
            {
                return message.GetString();
            }

            if (document.RootElement.TryGetProperty("detail", out var detail))
            {
                return detail.GetString();
            }
        }
        catch (JsonException)
        {
            return body;
        }

        return body;
    }
}

public sealed record RegisterCompanyResult(bool IsSuccess, string? ErrorMessage);

public sealed record CompanyActivationResult(bool IsSuccess, string Message);

public sealed record CompanyActivationPreviewResult(bool IsSuccess, CompanyActivationPreviewResponse? Preview, string? ErrorMessage);

public sealed record CompanyPortalLoginResult(bool IsSuccess, CompanyPortalSessionResponse? Session, string? ErrorMessage);

public sealed record EmployeeSaveResult(bool IsSuccess, string? ErrorMessage);

public sealed record EmployeeInvitationCodeResult(bool IsSuccess, CreateCompanyEmployeeResult? Invitation, string? ErrorMessage);

public sealed class CompanyEmployeeFormModel
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string Address { get; set; } = string.Empty;
    public string Gender { get; set; } = "Unspecified";
    public string EmploymentType { get; set; } = "CompanyEmployee";
    public int YearsOfExperience { get; set; }
    public decimal? MissionLatitude { get; set; }
    public decimal? MissionLongitude { get; set; }
    public int MissionRadiusKm { get; set; } = 5;
    public string ExperienceLevel { get; set; } = "Confirmed";
    public List<Guid> ServiceIds { get; } = [];
    public IBrowserFile? Photo { get; set; }
    public IBrowserFile? IdentityDocument { get; set; }
    public IBrowserFile? DiplomaDocument { get; set; }
}
