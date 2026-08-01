using HomeService.Client.Mobile.Pages;

namespace HomeService.Client.Mobile.Controls;

public partial class ClientNotificationButton : ContentView
{
    public ClientNotificationButton()
    {
        InitializeComponent();
    }

    private async void OnClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(ClientNotificationsPage));
    }
}
