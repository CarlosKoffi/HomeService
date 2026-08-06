using HomeService.Provider.Mobile.Services;

namespace HomeService.Provider.Mobile.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly ProviderSessionService? sessionService;

    public SettingsPage()
    {
        InitializeComponent();
        sessionService = IPlatformApplication.Current?.Services.GetService<ProviderSessionService>();
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    private void OnNotificationSettingsTapped(object? sender, TappedEventArgs e) => AppInfo.Current.ShowSettingsUI();

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        if (sessionService is not null) await sessionService.ClearAsync();
        if (Application.Current?.Windows.FirstOrDefault() is { } window) window.Page = new NavigationPage(new LoginPage());
    }
}
