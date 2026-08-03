using HomeService.Client.Mobile.Services;

namespace HomeService.Client.Mobile;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        UserAppTheme = AppTheme.Light;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());
        window.Resumed += OnWindowResumed;

#if WINDOWS
        window.Width = 430;
        window.Height = 850;
        window.MinimumWidth = 430;
        window.MinimumHeight = 850;
        window.MaximumWidth = 430;
        window.MaximumHeight = 850;
#endif

        return window;
    }

    private static async void OnWindowResumed(object? sender, EventArgs e)
    {
        try
        {
            var services = Current?.Handler?.MauiContext?.Services;
            if (services is null)
            {
                return;
            }

            await services.GetRequiredService<ClientDeviceRegistrationService>()
                .RegisterCurrentDeviceAsync();
            await services.GetRequiredService<ClientNotificationState>()
                .RefreshAsync();
        }
        catch (Exception)
        {
            // Returning to the foreground must remain possible when the network is unavailable.
        }
    }
}
