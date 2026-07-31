using HomeService.Client.Mobile.Pages;

namespace HomeService.Client.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
        Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
        Routing.RegisterRoute(nameof(MissionDetailPage), typeof(MissionDetailPage));
        Routing.RegisterRoute(nameof(CreateRequestPage), typeof(CreateRequestPage));
        Routing.RegisterRoute(nameof(CatalogSearchPage), typeof(CatalogSearchPage));
        Routing.RegisterRoute(nameof(PaymentMethodsPage), typeof(PaymentMethodsPage));
        Routing.RegisterRoute(nameof(AddPaymentMethodPage), typeof(AddPaymentMethodPage));
        Routing.RegisterRoute(nameof(ProfileInformationPage), typeof(ProfileInformationPage));
        Routing.RegisterRoute(nameof(AddressesPage), typeof(AddressesPage));
        Routing.RegisterRoute(nameof(ReviewsPage), typeof(ReviewsPage));
        Routing.RegisterRoute(nameof(ClientNotificationsPage), typeof(ClientNotificationsPage));
        Routing.RegisterRoute(nameof(ClientSettingsPage), typeof(ClientSettingsPage));

        Dispatcher.Dispatch(async () =>
        {
            await Task.Delay(100);
            if (!Preferences.Default.Get("ClientOnboardingSeen", false))
            {
                await GoToAsync("//onboarding");
            }
            else if (string.IsNullOrWhiteSpace(Preferences.Default.Get("ClientPhoneNumber", string.Empty)))
            {
                await GoToAsync("//welcome");
            }
            else
            {
                await GoToAsync("//home");
            }
        });
    }
}
