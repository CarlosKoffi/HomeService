using HomeService.Client.Mobile.Pages;
using HomeService.Client.Mobile.Services;

namespace HomeService.Client.Mobile.Controls;

public partial class ClientNotificationButton : ContentView
{
    private readonly ClientNotificationState state = MobileServiceLocator.GetRequiredService<ClientNotificationState>();

    public ClientNotificationButton()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        state.Changed += OnStateChanged;
        UpdateBadge();
        await state.RefreshAsync();
    }

    private void OnUnloaded(object? sender, EventArgs e)
        => state.Changed -= OnStateChanged;

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
