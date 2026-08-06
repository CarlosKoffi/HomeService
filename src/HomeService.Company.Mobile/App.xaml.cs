using HomeService.Company.Mobile.Pages;
using HomeService.Company.Mobile.Services;

namespace HomeService.Company.Mobile;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        UserAppTheme = AppTheme.Light;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new NavigationPage(new MainPage()));
        window.Resumed += OnWindowResumed;
        return window;
    }

    public static void ShowCompanyShell()
    {
        if (Current?.Windows.FirstOrDefault() is { } window)
        {
            window.Page = new AppShell();
        }
    }

    public static void ShowLogin()
    {
        if (Current?.Windows.FirstOrDefault() is { } window)
        {
            window.Page = new NavigationPage(new MainPage());
        }
    }

    private static async void OnWindowResumed(object? sender, EventArgs e)
    {
        try
        {
            var services = Current?.Handler?.MauiContext?.Services;
            if (services is not null)
            {
                await services.GetRequiredService<CompanyDeviceRegistrationService>()
                    .RegisterCurrentDeviceAsync();
            }
        }
        catch
        {
            // L'enregistrement des notifications sera retenté au prochain retour dans l'application.
        }
    }
}
