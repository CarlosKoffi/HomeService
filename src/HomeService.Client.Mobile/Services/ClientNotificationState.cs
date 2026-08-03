namespace HomeService.Client.Mobile.Services;

public sealed class ClientNotificationState(ClientMobileApiClient apiClient)
{
    public int UnreadCount { get; private set; }
    public event EventHandler? Changed;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var result = await apiClient.GetNotificationsAsync(true, cancellationToken);
        if (!result.IsSuccess || result.Response is null) return;
        SetUnreadCount(result.Response.UnreadCount);
    }

    public void SetUnreadCount(int unreadCount)
    {
        unreadCount = Math.Max(0, unreadCount);
        if (UnreadCount == unreadCount) return;
        UnreadCount = unreadCount;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Increment()
        => SetUnreadCount(UnreadCount + 1);
}
