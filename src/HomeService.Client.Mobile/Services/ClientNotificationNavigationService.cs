using HomeService.Client.Mobile.Pages;

namespace HomeService.Client.Mobile.Services;

public static class ClientNotificationNavigationService
{
    private const string PendingKey = "ClientPendingNotification";
    private static readonly string[] Keys = ["type", "missionId", "assignmentId", "quoteId", "conversationId"];

    public static void Store(IReadOnlyDictionary<string, string> data)
    {
        var values = Keys
            .Where(data.ContainsKey)
            .ToDictionary(key => key, key => data[key]);
        if (values.Count > 0)
        {
            Preferences.Default.Set(PendingKey, System.Text.Json.JsonSerializer.Serialize(values));
        }
    }

    public static async Task<bool> TryNavigateAsync()
    {
        var json = Preferences.Default.Get(PendingKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json) || Shell.Current is null)
        {
            return false;
        }

        var session = MobileServiceLocator.GetRequiredService<ClientSessionStore>();
        if (!await session.HasValidSessionAsync())
        {
            return false;
        }

        Dictionary<string, string>? data;
        try { data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json); }
        catch (System.Text.Json.JsonException) { data = null; }
        if (data is null || !data.TryGetValue("missionId", out var missionValue) || !Guid.TryParse(missionValue, out var missionId))
        {
            Preferences.Default.Remove(PendingKey);
            return false;
        }

        data.TryGetValue("type", out var type);
        var route = type switch
        {
            "mission_chat_message" => $"{nameof(MissionChatPage)}?missionId={missionId:D}",
            "mission_payment_required" => $"{nameof(PaymentCheckoutPage)}?missionId={missionId:D}",
            "mission_completed" => $"{nameof(MissionCompletionPage)}?missionId={missionId:D}",
            _ => $"{nameof(MissionDetailPage)}?missionId={missionId:D}"
        };

        Preferences.Default.Remove(PendingKey);
        await Shell.Current.GoToAsync(route);
        return true;
    }
}
