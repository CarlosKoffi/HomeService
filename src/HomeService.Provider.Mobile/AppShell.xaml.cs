namespace HomeService.Provider.Mobile;

using HomeService.Provider.Mobile.Pages;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(NotificationsPage), typeof(NotificationsPage));
        Routing.RegisterRoute(nameof(MissionDetailPage), typeof(MissionDetailPage));
        Routing.RegisterRoute(nameof(ProfileDetailsPage), typeof(ProfileDetailsPage));
        Routing.RegisterRoute(nameof(DocumentsPage), typeof(DocumentsPage));
        Routing.RegisterRoute(nameof(ServicesPage), typeof(ServicesPage));
        Routing.RegisterRoute(nameof(PortfolioPage), typeof(PortfolioPage));
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));

        Dispatcher.Dispatch(async () =>
        {
            await Task.Delay(150);
            await Services.ProviderNotificationNavigationService.TryNavigateAsync();
        });
    }
}
