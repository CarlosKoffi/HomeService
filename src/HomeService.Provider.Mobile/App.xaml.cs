namespace HomeService.Provider.Mobile;

using HomeService.Provider.Mobile.Pages;
using HomeService.Provider.Mobile.Services;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        UserAppTheme = AppTheme.Light;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(
            ProviderDeepLinkNavigationService.Wrap(
                ProviderDeepLinkNavigationService.CreateInitialPage()));
        window.Resumed += OnWindowResumed;
        return window;
    }

    private static async void OnWindowResumed(object? sender, EventArgs e)
    {
        try
        {
            await ProviderDeepLinkNavigationService.TryNavigateAsync();
        }
        catch
        {
            // A malformed external link must never block reopening the app.
        }

        try
        {
            var services = Current?.Handler?.MauiContext?.Services;
            if (services is not null)
            {
                await services.GetRequiredService<ProviderDeviceRegistrationService>()
                    .RegisterCurrentDeviceAsync();
            }
        }
        catch
        {
            // Device registration is retried later.
        }

        try
        {
            await ProviderNotificationNavigationService.TryNavigateAsync();
        }
        catch
        {
            // Notification navigation must never block reopening the app.
        }
    }
}
