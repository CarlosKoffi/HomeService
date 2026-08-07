using HomeService.Provider.Mobile.Pages;

namespace HomeService.Provider.Mobile.Services;

public static class ProviderNotificationNavigationService
{
    private const string PendingKey = "ProviderPendingNotification";
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
        if (services is null) return false;
        var token = await services.GetRequiredService<ProviderSessionService>().GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return false;

        Dictionary<string, string>? data;
        try { data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json); }
        catch (System.Text.Json.JsonException) { data = null; }
        if (data is null)
        {
            Preferences.Default.Remove(PendingKey);
            return false;
        }

        data.TryGetValue("type", out var type);
        if (!string.IsNullOrWhiteSpace(type)
            && (type.StartsWith("provider_affiliation_", StringComparison.Ordinal)
                || type.StartsWith("provider_profile_", StringComparison.Ordinal)))
        {
            Preferences.Default.Remove(PendingKey);
            await Shell.Current.GoToAsync("//profile");
            return true;
        }

        Guid assignmentId = Guid.Empty;
        if (data.TryGetValue("assignmentId", out var assignmentValue)) Guid.TryParse(assignmentValue, out assignmentId);
        if (assignmentId == Guid.Empty
            && data.TryGetValue("missionId", out var missionValue)
            && Guid.TryParse(missionValue, out var missionId))
        {
            var missions = await services.GetRequiredService<ProviderMobileApiClient>().GetMissionsAsync(token);
            assignmentId = missions.Response?.Items.FirstOrDefault(item => item.MissionId == missionId)?.AssignmentId ?? Guid.Empty;
        }

        if (assignmentId == Guid.Empty) return false;
        var route = type == "mission_chat_message"
            ? $"//messages?assignmentId={assignmentId:D}"
            : $"{nameof(MissionDetailPage)}?assignmentId={assignmentId:D}";
        Preferences.Default.Remove(PendingKey);
        await Shell.Current.GoToAsync(route);
        return true;
    }
}
