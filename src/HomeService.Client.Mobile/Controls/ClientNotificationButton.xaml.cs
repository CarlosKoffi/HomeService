using HomeService.Client.Mobile.Pages;
using HomeService.Client.Mobile.Services;

namespace HomeService.Client.Mobile.Controls;

public partial class ClientNotificationButton : ContentView
{
    private readonly ClientNotificationState state = MobileServiceLocator.GetRequiredService<ClientNotificationState>();
    private readonly ClientSessionStore sessionStore = MobileServiceLocator.GetRequiredService<ClientSessionStore>();
    private bool isSubscribed;

    public ClientNotificationButton()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        if (!isSubscribed)
        {
            state.Changed += OnStateChanged;
            isSubscribed = true;
        }

        UpdateBadge();
        try
        {
            await RefreshWhenSessionIsReadyAsync();
        }
        catch
        {
            // The notification badge is optional and must never close the current screen.
        }
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (!isSubscribed)
        {
            return;
        }

        state.Changed -= OnStateChanged;
        isSubscribed = false;
    }

    private async Task RefreshWhenSessionIsReadyAsync()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (await sessionStore.HasValidSessionAsync())
            {
                await state.RefreshAsync();
                UpdateBadge();
                return;
            }

            await Task.Delay(350);
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
        => MainThread.BeginInvokeOnMainThread(UpdateBadge);

    private void UpdateBadge()
    {
        UnreadBadge.IsVisible = state.UnreadCount > 0;
        UnreadBadgeLabel.Text = state.UnreadCount > 99 ? "99+" : state.UnreadCount.ToString();
    }

    private async void OnClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(ClientNotificationsPage));
    }
}
