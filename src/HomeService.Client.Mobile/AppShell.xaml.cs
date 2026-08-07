using HomeService.Client.Mobile.Pages;
using HomeService.Client.Mobile.Services;

namespace HomeService.Client.Mobile;

public partial class AppShell : Shell
{
    private static readonly TimeSpan BadgeRefreshInterval = TimeSpan.FromSeconds(12);
    private readonly SemaphoreSlim badgeRefreshGate = new(1, 1);

    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
        Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
        Routing.RegisterRoute(nameof(MissionDetailPage), typeof(MissionDetailPage));
        Routing.RegisterRoute(nameof(MissionChatPage), typeof(MissionChatPage));
        Routing.RegisterRoute(nameof(CreateRequestPage), typeof(CreateRequestPage));
        Routing.RegisterRoute(nameof(CatalogSearchPage), typeof(CatalogSearchPage));
        Routing.RegisterRoute(nameof(PaymentMethodsPage), typeof(PaymentMethodsPage));
        Routing.RegisterRoute(nameof(PaymentCheckoutPage), typeof(PaymentCheckoutPage));
        Routing.RegisterRoute(nameof(PaymentSuccessPage), typeof(PaymentSuccessPage));
        Routing.RegisterRoute(nameof(AddPaymentMethodPage), typeof(AddPaymentMethodPage));
        Routing.RegisterRoute(nameof(ProfileInformationPage), typeof(ProfileInformationPage));
        Routing.RegisterRoute(nameof(AddressesPage), typeof(AddressesPage));
        Routing.RegisterRoute(nameof(ReviewsPage), typeof(ReviewsPage));
        Routing.RegisterRoute("profile-requests", typeof(RequestsPage));
        Routing.RegisterRoute(nameof(ClientNotificationsPage), typeof(ClientNotificationsPage));
        Routing.RegisterRoute(nameof(ClientSettingsPage), typeof(ClientSettingsPage));
        Routing.RegisterRoute(nameof(ProviderTrackingPage), typeof(ProviderTrackingPage));
        Routing.RegisterRoute(nameof(MissionCompletionPage), typeof(MissionCompletionPage));
        Routing.RegisterRoute(nameof(MissionRatingPage), typeof(MissionRatingPage));
        Routing.RegisterRoute(nameof(MissionReviewPhotosPage), typeof(MissionReviewPhotosPage));
        Routing.RegisterRoute(nameof(MissionReviewSuccessPage), typeof(MissionReviewSuccessPage));

        Dispatcher.Dispatch(async () =>
        {
            await Task.Delay(100);
            if (Preferences.Default.Get("ClientPreviewMode", false))
            {
                await GoToAsync("//home");
            }
            else if (!Preferences.Default.Get("ClientOnboardingSeen", false))
            {
                await GoToAsync("//onboarding");
            }
            else if (!await MobileServiceLocator.GetRequiredService<ClientSessionStore>().HasValidSessionAsync())
            {
                await MobileServiceLocator.GetRequiredService<ClientSessionStore>().ClearAsync();
                await GoToAsync("//welcome");
            }
            else
            {
                await GoToAsync("//home");
            }

            await ClientNotificationNavigationService.TryNavigateAsync();
            _ = RunBadgeRefreshLoopAsync();
        });
    }

    public async Task RefreshNavigationBadgesAsync()
    {
        if (!await badgeRefreshGate.WaitAsync(0)) return;
        try
        {
            var apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
            var sessionStore = MobileServiceLocator.GetRequiredService<ClientSessionStore>();
            if (!await sessionStore.HasValidSessionAsync())
            {
                SetBadgeTitles(0, 0);
                return;
            }

            var result = await apiClient.GetNavigationBadgesAsync();
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

    private void SetBadgeTitles(int requestCount, int messageCount)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RequestsTab.Title = BadgeTitle("Demandes", requestCount);
            MessagesTab.Title = BadgeTitle("Messages", messageCount);
        });
    }

    private static string BadgeTitle(string title, int count)
        => count <= 0 ? title : $"{title} ({Math.Min(count, 99)}{(count > 99 ? "+" : string.Empty)})";
}
