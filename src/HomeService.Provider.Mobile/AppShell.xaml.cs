namespace HomeService.Provider.Mobile;

using HomeService.Provider.Mobile.Pages;

public partial class AppShell : Shell
{
    private static readonly TimeSpan BadgeRefreshInterval = TimeSpan.FromSeconds(12);
    private readonly SemaphoreSlim badgeRefreshGate = new(1, 1);

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
            _ = RunBadgeRefreshLoopAsync();
        });
    }

    public async Task RefreshNavigationBadgesAsync()
    {
        if (!await badgeRefreshGate.WaitAsync(0)) return;
        try
        {
            var services = IPlatformApplication.Current?.Services;
            var apiClient = services?.GetService<Services.ProviderMobileApiClient>();
            var sessionService = services?.GetService<Services.ProviderSessionService>();
            if (apiClient is null || sessionService is null) return;
            var token = await sessionService.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                SetBadgeTitles(0, 0);
                return;
            }

            var result = await apiClient.GetNavigationBadgesAsync(token);
            if (result.IsSuccess && result.Response is not null)
            {
                SetBadgeTitles(result.Response.ActionCount, result.Response.MessageCount);
            }
        }
        finally
        {
            badgeRefreshGate.Release();
        }
    }

    private async Task RunBadgeRefreshLoopAsync()
    {
        await RefreshNavigationBadgesAsync();
        using var timer = new PeriodicTimer(BadgeRefreshInterval);
        while (await timer.WaitForNextTickAsync())
        {
            await RefreshNavigationBadgesAsync();
        }
    }

    private void SetBadgeTitles(int missionCount, int messageCount)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            MissionsTab.Title = BadgeTitle("Missions", missionCount);
            MessagesTab.Title = BadgeTitle("Messages", messageCount);
        });
    }

    private static string BadgeTitle(string title, int count)
        => count <= 0 ? title : $"{title} ({Math.Min(count, 99)}{(count > 99 ? "+" : string.Empty)})";
}
