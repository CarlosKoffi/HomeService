using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HomeService.Contracts.Branding;
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

    public async Task<CountryBrandingResponse?> GetCountryBrandingAsync(string countryCode = "CI", CancellationToken cancellationToken = default)
    {
        AddBasicAuthIfConfigured();
        return await httpClient.GetFromJsonAsync<CountryBrandingResponse>($"/api/country-branding?country={Uri.EscapeDataString(countryCode)}", cancellationToken);
    }

    public async Task<RegisterCompanyResult> RegisterCompanyAsync(
        RegisterCompanyRequest request,
        IReadOnlyDictionary<string, IBrowserFile?> documents,
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

    private bool IsAuthEnabled()
    {
        var value = configuration["SITE_AUTH_ENABLED"];
        return !string.Equals(value?.Trim(), "false", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(value?.Trim(), "0", StringComparison.OrdinalIgnoreCase);
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
