using HomeService.Company.Mobile.Pages;

namespace HomeService.Company.Mobile.Services;

public static class CompanyNotificationNavigationService
{
    private const string PendingKey = "CompanyPendingNotification";
    private static readonly string[] Keys = ["type", "missionId", "assignmentId", "quoteId", "conversationId", "providerId", "requestId"];

    public static void Store(IReadOnlyDictionary<string, string> data)
    {
        var values = Keys.Where(data.ContainsKey).ToDictionary(key => key, key => data[key]);
        if (values.Count > 0) Preferences.Default.Set(PendingKey, System.Text.Json.JsonSerializer.Serialize(values));
    }

    public static async Task<bool> TryNavigateAsync()
    {
        var json = Preferences.Default.Get(PendingKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json) || Shell.Current is null) return false;
        var services = IPlatformApplication.Current?.Services;
        if (services is null || string.IsNullOrWhiteSpace(await services.GetRequiredService<CompanySessionStore>().GetTokenAsync())) return false;

        Dictionary<string, string>? data;
        try { data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json); }
        catch (System.Text.Json.JsonException) { data = null; }
        if (data is null)
        {
            Preferences.Default.Remove(PendingKey);
            return false;
        }

        data.TryGetValue("type", out var type);
        string? route = null;
        if (!string.IsNullOrWhiteSpace(type) && (type.Contains("provider_validation", StringComparison.OrdinalIgnoreCase)
            || type.Contains("provider_application", StringComparison.OrdinalIgnoreCase)))
        {
            data.TryGetValue("requestId", out var requestValue);
            data.TryGetValue("providerId", out var providerValue);
            route = $"{nameof(ProviderCandidateDetailPage)}?requestId={Uri.EscapeDataString(requestValue ?? string.Empty)}&providerId={Uri.EscapeDataString(providerValue ?? string.Empty)}";
        }
        else if (data.TryGetValue("missionId", out var missionValue) && Guid.TryParse(missionValue, out var missionId))
        {
            route = type == "mission_chat_message"
                ? $"{nameof(ChatPage)}?missionId={missionId:D}"
                : $"{nameof(MissionDetailPage)}?missionId={missionId:D}";
        }

        if (route is null) return false;
        Preferences.Default.Remove(PendingKey);
        await Shell.Current.GoToAsync(route);
        return true;
    }
}
