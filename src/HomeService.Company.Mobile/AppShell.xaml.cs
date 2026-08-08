using HomeService.Company.Mobile.Pages;

namespace HomeService.Company.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(MissionDetailPage), typeof(MissionDetailPage));
        Routing.RegisterRoute(nameof(ProviderCandidateDetailPage), typeof(ProviderCandidateDetailPage));
        Routing.RegisterRoute(nameof(ChatPage), typeof(ChatPage));

        Dispatcher.Dispatch(async () =>
        {
            await Task.Delay(150);
            await Services.CompanyNotificationNavigationService.TryNavigateAsync();
        });
    }

    public Task RefreshNavigationBadgesAsync() => Task.CompletedTask;
}
