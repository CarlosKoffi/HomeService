using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using HomeService.Contracts.Companies;
using HomeService.Contracts.Localization;
using HomeService.Contracts.Services;
using Microsoft.AspNetCore.Components.Forms;

namespace HomeService.Company.Services;

public sealed class PlatformApiClient(HttpClient httpClient, IConfiguration configuration)
{
    private const long MaxUploadSize = 10 * 1024 * 1024;

    public async Task<IReadOnlyList<ServiceSummaryResponse>> GetServicesAsync(CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await httpClient.GetFromJsonAsync<IReadOnlyList<ServiceSummaryResponse>>("/api/services", cancellationToken) ?? [];
    }

    public async Task<bool> RegisterCompanyAsync(
        RegisterCompanyRequest request,
        IReadOnlyDictionary<string, IBrowserFile?> documents,
        CancellationToken cancellationToken = default)
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
        AddString(content, "estimatedProviderCount", request.EstimatedProviderCount?.ToString());

        foreach (var service in request.Services)
        {
            AddString(content, "services", service);
        }

        foreach (var (fieldName, file) in documents)
        {
            if (file is null)
            {
                continue;
            }

            var fileContent = new StreamContent(file.OpenReadStream(MaxUploadSize));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            content.Add(fileContent, fieldName, file.Name);
        }

        var response = await httpClient.PostAsync("/api/company-applications", content, cancellationToken);
        return response.IsSuccessStatusCode;
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

    private static void AddString(MultipartFormDataContent content, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            content.Add(new StringContent(value, Encoding.UTF8), name);
        }
    }
}
