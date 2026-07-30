using HomeService.Client.Mobile.Pages;

namespace HomeService.Client.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
        Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
        Routing.RegisterRoute(nameof(OnboardingPage), typeof(OnboardingPage));
        Routing.RegisterRoute(nameof(WelcomePage), typeof(WelcomePage));
        Routing.RegisterRoute(nameof(MissionDetailPage), typeof(MissionDetailPage));
        Routing.RegisterRoute(nameof(CreateRequestPage), typeof(CreateRequestPage));

        Dispatcher.Dispatch(async () =>
        {
            await Task.Delay(100);
            if (!Preferences.Default.Get("ClientOnboardingSeen", false))
            {
                await GoToAsync(nameof(OnboardingPage));
            }
            else if (string.IsNullOrWhiteSpace(Preferences.Default.Get("ClientPhoneNumber", string.Empty)))
            {
                await GoToAsync(nameof(WelcomePage));
            }
        });
    }
}
