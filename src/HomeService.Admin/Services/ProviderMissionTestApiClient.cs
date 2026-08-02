using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using HomeService.Contracts.Admin;

namespace HomeService.Admin.Services;

public sealed class ProviderMissionTestApiClient(
    HttpClient httpClient,
    IConfiguration configuration,
    AdminApiSessionAccessor sessionAccessor)
{
    public async Task<AdminProviderMissionTestListResponse?> GetAssignmentsAsync(CancellationToken cancellationToken = default)
    {
        AddAuthentication();
        using var response = await httpClient.GetAsync("/api/admin/missions/provider-test/assignments", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AdminProviderMissionTestListResponse>(cancellationToken);
    }

    public async Task<AdminProviderMissionTestActionResponse?> AcceptAsync(Guid assignmentId, CancellationToken cancellationToken = default)
        => await SendActionAsync(assignmentId, "validate", cancellationToken);

    public async Task<AdminProviderMissionTestActionResponse?> StartAsync(Guid assignmentId, CancellationToken cancellationToken = default)
        => await SendActionAsync(assignmentId, "start", cancellationToken);

    public async Task<AdminProviderMissionTestActionResponse?> CompleteAsync(Guid assignmentId, CancellationToken cancellationToken = default)
        => await SendActionAsync(assignmentId, "complete", cancellationToken);

    private async Task<AdminProviderMissionTestActionResponse?> SendActionAsync(
        Guid assignmentId,
        string action,
        CancellationToken cancellationToken)
    {
        AddAuthentication();
        var path = $"/api/admin/missions/provider-test/assignments/{assignmentId}/{action}";
        using var response = await httpClient.PostAsync(path, null, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<AdminProviderMissionTestActionResponse>(cancellationToken);
        }

        var failure = await response.Content.ReadFromJsonAsync<AdminProviderMissionTestActionResponse>(cancellationToken);
        throw new PlatformApiException(failure?.Message ?? $"La validation a echoue ({(int)response.StatusCode}).");
    }

    private void AddAuthentication()
    {
        httpClient.DefaultRequestHeaders.Remove("X-Admin-Session");
        if (!string.IsNullOrWhiteSpace(sessionAccessor.Token))
        {
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Admin-Session", sessionAccessor.Token);
        }

        var authEnabled = !string.Equals(configuration["SITE_AUTH_ENABLED"]?.Trim(), "false", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(configuration["SITE_AUTH_ENABLED"]?.Trim(), "0", StringComparison.OrdinalIgnoreCase);
        var password = configuration["SITE_AUTH_PASSWORD"]?.Trim();
        if (!authEnabled || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var username = (configuration["SITE_AUTH_USERNAME"] ?? "admin").Trim();
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new PlatformApiException($"Impossible de charger les missions de test ({(int)response.StatusCode}). {body}");
    }
}
