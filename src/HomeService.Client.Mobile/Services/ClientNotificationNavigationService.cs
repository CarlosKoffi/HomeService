using HomeService.Client.Mobile.Pages;
using HomeService.Contracts.Clients;

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
        var route = await ResolveMissionRouteAsync(missionId, type);

        Preferences.Default.Remove(PendingKey);
        await Shell.Current.GoToAsync(route);
        return true;
    }

    public static async Task<string> ResolveMissionRouteAsync(
        Guid missionId,
        string? notificationType,
        CancellationToken cancellationToken = default)
    {
        var detailRoute = $"{nameof(MissionDetailPage)}?missionId={missionId:D}";
        ClientMissionStatusResponse? mission = null;

        try
        {
            var apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
            var result = await apiClient.GetMissionAsync(missionId, cancellationToken);
            if (result.IsSuccess)
            {
                mission = result.Response;
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or OperationCanceledException)
        {
            // A stale action must never be opened when the current mission state cannot be verified.
        }

        if (mission is null || IsClosed(mission.Status))
        {
            return detailRoute;
        }

        if (string.Equals(notificationType, "mission_payment_required", StringComparison.OrdinalIgnoreCase))
        {
            return mission.Actions.CanAcceptQuote
                ? $"{nameof(PaymentCheckoutPage)}?missionId={missionId:D}"
                : detailRoute;
        }

        if (string.Equals(notificationType, "mission_chat_message", StringComparison.OrdinalIgnoreCase))
        {
            return IsConversationActive(mission.Status)
                ? $"{nameof(MissionChatPage)}?missionId={missionId:D}"
                : detailRoute;
        }

        // A completion notification is informative. The detail page reflects the current
        // status and only exposes a completion action when the API still authorizes it.
        return detailRoute;
    }

    private static bool IsClosed(string status)
        => status.Equals("Completed", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Canceled", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Disputed", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Resolved", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Rejected", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Expired", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Failed", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Refunded", StringComparison.OrdinalIgnoreCase);

    private static bool IsConversationActive(string status)
        => status.Equals("Accepted", StringComparison.OrdinalIgnoreCase)
            || status.Equals("OnTheWay", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Started", StringComparison.OrdinalIgnoreCase);
}
