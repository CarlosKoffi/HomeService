using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HomeService.Application.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HomeService.Infrastructure.Notifications;

public sealed class FirebaseCloudMessagingSender(
    HttpClient httpClient,
    IOptions<FirebaseOptions> optionsAccessor,
    ILogger<FirebaseCloudMessagingSender> logger) : IMobilePushSender
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly FirebaseOptions options = optionsAccessor.Value;
    private string? cachedAccessToken;
    private DateTimeOffset cachedAccessTokenExpiresAt;

    public async Task<MobilePushSendResult> SendAsync(
        string deviceToken,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data,
        CancellationToken cancellationToken)
    {
        if (!options.Enabled)
        {
            logger.LogWarning("Firebase mobile push is disabled.");
            return MobilePushSendResult.Failed("Firebase notifications disabled.");
        }

        if (string.IsNullOrWhiteSpace(options.ProjectId) || string.IsNullOrWhiteSpace(options.CredentialsJson))
        {
            logger.LogError(
                "Firebase configuration is incomplete. ProjectId configured: {HasProjectId}; credentials configured: {HasCredentials}.",
                !string.IsNullOrWhiteSpace(options.ProjectId),
                !string.IsNullOrWhiteSpace(options.CredentialsJson));
            return MobilePushSendResult.Failed("Firebase configuration incomplete.");
        }

        var accessToken = await GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            logger.LogError("Firebase access token could not be generated for project {ProjectId}.", options.ProjectId);
            return MobilePushSendResult.Failed("Firebase access token unavailable.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://fcm.googleapis.com/v1/projects/{options.ProjectId}/messages:send");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new
        {
            message = new
            {
                token = deviceToken,
                notification = new { title, body },
                data = data ?? new Dictionary<string, string>()
            }
        });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError(
                "Firebase rejected a mobile push with status {StatusCode}: {ResponseBody}",
                (int)response.StatusCode,
                responseBody);
            return MobilePushSendResult.Failed($"Firebase {(int)response.StatusCode}: {responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);
        var name = document.RootElement.TryGetProperty("name", out var nameElement)
            ? nameElement.GetString()
            : null;

        return MobilePushSendResult.Sent(name);
    }

    private async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(cachedAccessToken)
            && cachedAccessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            return cachedAccessToken;
        }

        var credentials = JsonSerializer.Deserialize<FirebaseServiceAccountCredentials>(
            options.CredentialsJson!,
            JsonOptions);
        if (credentials is null
            || string.IsNullOrWhiteSpace(credentials.ClientEmail)
            || string.IsNullOrWhiteSpace(credentials.PrivateKey)
            || string.IsNullOrWhiteSpace(credentials.TokenUri))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var assertion = CreateJwtAssertion(credentials, now);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = assertion
        });

        using var response = await httpClient.PostAsync(credentials.TokenUri, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<GoogleAccessTokenResponse>(
            JsonOptions,
            cancellationToken);
        cachedAccessToken = tokenResponse?.AccessToken;
        cachedAccessTokenExpiresAt = now.AddSeconds(Math.Max(60, tokenResponse?.ExpiresIn ?? 3600));
        return cachedAccessToken;
    }

    private static string CreateJwtAssertion(FirebaseServiceAccountCredentials credentials, DateTimeOffset now)
    {
        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            alg = "RS256",
            typ = "JWT"
        }));
        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = credentials.ClientEmail,
            scope = "https://www.googleapis.com/auth/firebase.messaging",
            aud = credentials.TokenUri,
            iat = now.ToUnixTimeSeconds(),
            exp = now.AddMinutes(55).ToUnixTimeSeconds()
        }));

        var unsignedToken = $"{header}.{payload}";
        using var rsa = RSA.Create();
        rsa.ImportFromPem(credentials.PrivateKey);
        var signature = rsa.SignData(
            Encoding.UTF8.GetBytes(unsignedToken),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return $"{unsignedToken}.{Base64UrlEncode(signature)}";
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private sealed record FirebaseServiceAccountCredentials(
        [property: JsonPropertyName("type")]
        string Type,
        [property: JsonPropertyName("project_id")]
        string ProjectId,
        [property: JsonPropertyName("private_key_id")]
        string PrivateKeyId,
        [property: JsonPropertyName("private_key")]
        string PrivateKey,
        [property: JsonPropertyName("client_email")]
        string ClientEmail,
        [property: JsonPropertyName("client_id")]
        string ClientId,
        [property: JsonPropertyName("auth_uri")]
        string AuthUri,
        [property: JsonPropertyName("token_uri")]
        string TokenUri,
        [property: JsonPropertyName("auth_provider_x509_cert_url")]
        string AuthProviderX509CertUrl,
        [property: JsonPropertyName("client_x509_cert_url")]
        string ClientX509CertUrl,
        [property: JsonPropertyName("universe_domain")]
        string UniverseDomain);

    private sealed record GoogleAccessTokenResponse(
        [property: JsonPropertyName("access_token")]
        string AccessToken,
        [property: JsonPropertyName("expires_in")]
        int ExpiresIn,
        [property: JsonPropertyName("token_type")]
        string TokenType);
}
