using HomeService.Company.Mobile.Pages;

namespace HomeService.Company.Mobile;

public partial class AppShell : Shell
{
    private static readonly TimeSpan BadgeRefreshInterval = TimeSpan.FromSeconds(12);
    private readonly SemaphoreSlim badgeRefreshGate = new(1, 1);

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
            _ = RunBadgeRefreshLoopAsync();
        });
    }

    public async Task RefreshNavigationBadgesAsync()
    {
        if (!await badgeRefreshGate.WaitAsync(0)) return;
        try
        {
            var services = IPlatformApplication.Current?.Services;
            var apiClient = services?.GetService<Services.CompanyMobileApiClient>();
            var sessionStore = services?.GetService<Services.CompanySessionStore>();
            if (apiClient is null || sessionStore is null) return;

            var token = await sessionStore.GetTokenAsync();
            var companyId = await sessionStore.GetCompanyIdAsync();
            if (string.IsNullOrWhiteSpace(token) || !companyId.HasValue)
            {
                SetBadgeTitles(0, 0);
                return;
            }

            var result = await apiClient.GetNavigationBadgesAsync(token, companyId.Value);
            if (result.IsSuccess && result.Response is not null)
            {
                SetBadgeTitles(result.Response.MessageCount, result.Response.AlertCount);
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

    private void SetBadgeTitles(int messageCount, int alertCount)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            MessagesTab.Title = BadgeTitle("Messages", messageCount);
            AlertsTab.Title = BadgeTitle("Alertes", alertCount);
        });
    }

    private static string BadgeTitle(string title, int count)
        => count <= 0 ? title : $"{title} ({Math.Min(count, 99)}{(count > 99 ? "+" : string.Empty)})";
}
