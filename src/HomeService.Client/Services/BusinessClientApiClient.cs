using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HomeService.Contracts.BusinessClients;
using HomeService.Contracts.Clients;
using Microsoft.AspNetCore.Components.Forms;

namespace HomeService.Client.Services;

public sealed class BusinessClientApiClient(HttpClient httpClient, ILogger<BusinessClientApiClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<BusinessClientApiResult<ClientAuthResponse>> RegisterAsync(
        RegisterClientRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<ClientAuthResponse>(HttpMethod.Post, "api/client/business/auth/register", null, request, cancellationToken);

    public Task<BusinessClientApiResult<ClientAuthResponse>> LoginAsync(
        LoginClientRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<ClientAuthResponse>(HttpMethod.Post, "api/client/business/auth/login", null, request, cancellationToken);

    public Task<BusinessClientApiResult<BusinessClientProfileResponse>> GetProfileAsync(
        string token,
        CancellationToken cancellationToken = default) =>
        SendAsync<BusinessClientProfileResponse>(HttpMethod.Get, "api/client/business/profile", token, null, cancellationToken);

    public Task<BusinessClientApiResult<BusinessClientProfileResponse>> SaveProfileAsync(
        string token,
        UpsertBusinessClientProfileRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<BusinessClientProfileResponse>(HttpMethod.Put, "api/client/business/profile", token, request, cancellationToken);

    public Task<BusinessClientApiResult<BusinessClientProfileResponse>> SubmitAsync(
        string token,
        CancellationToken cancellationToken = default) =>
        SendAsync<BusinessClientProfileResponse>(HttpMethod.Post, "api/client/business/submit", token, new { }, cancellationToken);

    public async Task<BusinessClientApiResult<BusinessClientDocumentResponse>> UploadDocumentAsync(
        string token,
        string documentType,
        IBrowserFile file,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(documentType), "documentType");
            var stream = file.OpenReadStream(25 * 1024 * 1024, cancellationToken);
            using var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
            form.Add(fileContent, "file", file.Name);

            using var request = CreateRequest(HttpMethod.Post, "api/client/business/documents", token);
            request.Content = form;
            using var response = await httpClient.SendAsync(request, cancellationToken);
            return await ReadResponseAsync<BusinessClientDocumentResponse>(response, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException or InvalidOperationException)
        {
            logger.LogWarning(exception, "Business client document upload failed.");
            return BusinessClientApiResult<BusinessClientDocumentResponse>.Failed(
                "Le document n’a pas pu être envoyé. Vérifiez sa taille et réessayez.");
        }
    }

    public async Task<BusinessClientApiResult<bool>> DeleteDocumentAsync(
        string token,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Delete, $"api/client/business/documents/{documentId}", token);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return BusinessClientApiResult<bool>.Ok(true);
            }

            return BusinessClientApiResult<bool>.Failed(await ReadErrorAsync(response, cancellationToken), response.StatusCode);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Business client document deletion failed.");
            return BusinessClientApiResult<bool>.Failed("La suppression n’a pas pu être effectuée.");
        }
    }

    public async Task<BusinessClientFileResult?> DownloadDocumentAsync(
        string token,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, $"api/client/business/documents/{documentId}/download", token);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                ?? "document";
            return new BusinessClientFileResult(
                bytes,
                response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream",
                fileName);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Business client document download failed.");
            return null;
        }
    }

    private async Task<BusinessClientApiResult<T>> SendAsync<T>(
        HttpMethod method,
        string path,
        string? token,
        object? payload,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = CreateRequest(method, path, token);
            if (payload is not null)
            {
                request.Content = JsonContent.Create(payload, options: JsonOptions);
            }

            using var response = await httpClient.SendAsync(request, cancellationToken);
            return await ReadResponseAsync<T>(response, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(exception, "Business client API request failed for {Path}.", path);
            return BusinessClientApiResult<T>.Failed("Le service est momentanément indisponible. Réessayez dans quelques instants.");
        }
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, string? token)
    {
        var request = new HttpRequestMessage(method, path);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    private static async Task<BusinessClientApiResult<T>> ReadResponseAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return value is null
                ? BusinessClientApiResult<T>.Failed("La réponse reçue est incomplète.", response.StatusCode)
                : BusinessClientApiResult<T>.Ok(value);
        }

        return BusinessClientApiResult<T>.Failed(await ReadErrorAsync(response, cancellationToken), response.StatusCode);
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<BusinessClientApiError>(JsonOptions, cancellationToken);
            var details = error?.Errors is { Length: > 0 }
                ? string.Join(" ", error.Errors)
                : null;
            return details ?? error?.Message ?? "L’opération n’a pas pu être effectuée.";
        }
        catch
        {
            return response.StatusCode == HttpStatusCode.Unauthorized
                ? "Votre session a expiré. Reconnectez-vous."
                : "L’opération n’a pas pu être effectuée.";
        }
    }
}

public sealed record BusinessClientApiResult<T>(bool IsSuccess, string? ErrorMessage, T? Value, HttpStatusCode? StatusCode)
{
    public bool IsNotFound => StatusCode == HttpStatusCode.NotFound;
    public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;
    public static BusinessClientApiResult<T> Ok(T value) => new(true, null, value, HttpStatusCode.OK);
    public static BusinessClientApiResult<T> Failed(string message, HttpStatusCode? statusCode = null) =>
        new(false, message, default, statusCode);
}

public sealed record BusinessClientApiError(string? Message, string[]? Errors);
public sealed record BusinessClientFileResult(byte[] Bytes, string ContentType, string FileName);
