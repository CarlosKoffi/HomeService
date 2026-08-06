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
        var window = new Window(new NavigationPage(new LoginPage())
        {
            BarBackgroundColor = Colors.White,
            BarTextColor = Color.FromArgb("#0F172A")
        });
        window.Resumed += OnWindowResumed;
        return window;
    }

    private static async void OnWindowResumed(object? sender, EventArgs e)
    {
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
            // A notification registration failure must never block reopening the app.
        }
    }
}
