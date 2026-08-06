using HomeService.Contracts.ProviderPortal;
using HomeService.Provider.Mobile.Services;
using Microsoft.Maui.Devices.Sensors;

namespace HomeService.Provider.Mobile.Pages;

public partial class HomePage : ContentPage
{
    private readonly ProviderMobileApiClient? apiClient;
    private readonly ProviderSessionService? sessionService;
    private ProviderMobileMissionOfferResponse? currentLiveOffer;
    private ProviderMobileMissionSummaryResponse? currentUpcomingMission;
    private string? accessToken;
    private CancellationTokenSource? offerCountdownCancellation;
    private bool offerActionInProgress;
    private bool renderingAvailability;

    public HomePage()
    {
        InitializeComponent();
        apiClient = IPlatformApplication.Current?.Services.GetService<ProviderMobileApiClient>();
        sessionService = IPlatformApplication.Current?.Services.GetService<ProviderSessionService>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadHomeAsync();
    }

    protected override void OnDisappearing()
    {
        StopOfferCountdown();
        base.OnDisappearing();
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadHomeAsync();
        RefreshHost.IsRefreshing = false;
    }

    private async Task LoadHomeAsync()
    {
        if (apiClient is null || sessionService is null)
        {
            ShowMessage("Configuration mobile incomplète.");
            return;
        }

        accessToken = await sessionService.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            await ReturnToLoginAsync();
            return;
        }

        SetBusy(true);
        var homeTask = apiClient.GetHomeResultAsync(accessToken);
        var missionsTask = apiClient.GetMissionsAsync(accessToken);
        var notificationsTask = apiClient.GetNotificationsAsync(accessToken, true);
        await Task.WhenAll(homeTask, missionsTask, notificationsTask);
        var homeResult = await homeTask;
        var missionsResult = await missionsTask;
        var notificationsResult = await notificationsTask;
        SetBusy(false);

        if (!homeResult.IsSuccess || homeResult.Response is null)
        {
            if (homeResult.StatusCode == 401)
            {
                await ReturnToLoginAsync();
                return;
            }
            ShowMessage(homeResult.ErrorMessage ?? "Impossible de charger vos missions.");
            return;
        }

        MessageBanner.IsVisible = false;
        RenderHome(homeResult.Response, missionsResult.Response?.Items ?? [], notificationsResult.Response?.UnreadCount ?? 0);
    }

    private void RenderHome(ProviderMobileHomeResponse home, IReadOnlyList<ProviderMobileMissionSummaryResponse> missions, int unreadCount)
    {
        GreetingLabel.Text = $"Bonjour {FirstName(home.Status.DisplayName)} 👋";
        ProviderStatusLabel.Text = home.Status.CompanyName;
        renderingAvailability = true;
        AvailabilitySwitch.IsToggled = home.Status.IsAvailable;
        AvailabilitySwitch.IsEnabled = home.Status.CanChangeAvailability;
        renderingAvailability = false;
        AvailabilityLabel.Text = home.Status.AvailabilityLabel;
        AvailabilityLabel.TextColor = Color.FromArgb(home.Status.IsAvailable ? "#16B364" : "#DC2626");
        AvailabilityInfoCard.IsVisible = !home.Status.CanChangeAvailability;
        AvailabilityMessageLabel.Text = home.Status.AvailabilityMessage;

        NotificationBadge.IsVisible = unreadCount > 0;
        NotificationBadgeLabel.Text = unreadCount > 99 ? "99+" : unreadCount.ToString();

        ProfileCompletionBanner.IsVisible = home.ProfileCompletion is not null;
        if (home.ProfileCompletion is not null)
        {
            ProfilePercentLabel.Text = $"{home.ProfileCompletion.Percent}%";
            ProfileMessageLabel.Text = home.ProfileCompletion.Message;
        }

        var now = DateTime.Now;
        TodayCountLabel.Text = missions.Count(item => item.ScheduledFor?.LocalDateTime.Date == now.Date).ToString();
        ActiveCountLabel.Text = missions.Count(item => item.Status == "Started" || (item.Status == "Accepted" && item.ScheduledFor?.LocalDateTime <= now)).ToString();
        UpcomingCountLabel.Text = missions.Count(item => item.Status == "Accepted" && item.ScheduledFor?.LocalDateTime > now).ToString();

        currentUpcomingMission = home.UpcomingMission;
        if (currentUpcomingMission is null)
        {
            UpcomingMissionIcon.Source = "icon_calendar.svg";
            UpcomingMissionTitleLabel.Text = "Aucune mission planifiée";
            UpcomingMissionDetailLabel.Text = "Les prochains rendez-vous apparaîtront ici.";
        }
        else
        {
            UpcomingMissionIcon.Source = ProviderIconResolver.ForService(currentUpcomingMission.ServiceIconName, currentUpcomingMission.ServiceName);
            UpcomingMissionTitleLabel.Text = string.IsNullOrWhiteSpace(currentUpcomingMission.PrestationName) ? currentUpcomingMission.ServiceName : currentUpcomingMission.PrestationName;
            UpcomingMissionDetailLabel.Text = $"{FormatMissionTime(currentUpcomingMission.ScheduledFor)} · {currentUpcomingMission.LocationLabel}";
        }

        currentLiveOffer = home.LiveOffer;
        LiveOfferCard.IsVisible = currentLiveOffer is not null;
        StopOfferCountdown();
        if (currentLiveOffer is not null)
        {
            LiveOfferIcon.Source = ProviderIconResolver.ForService(currentLiveOffer.ServiceIconName, currentLiveOffer.ServiceName);
            LiveOfferTitleLabel.Text = currentLiveOffer.ServiceName;
            LiveOfferDetailLabel.Text = $"{currentLiveOffer.LocationLabel} · {FormatDistance(currentLiveOffer.DistanceKm)}";
            UpdateOfferCountdown(currentLiveOffer.ExpiresAt);
            SetBusy(false);
            StartOfferCountdown(currentLiveOffer.AssignmentId, currentLiveOffer.ExpiresAt);
        }
    }

    private async void OnAvailabilityToggled(object? sender, ToggledEventArgs e)
    {
        if (renderingAvailability || apiClient is null || string.IsNullOrWhiteSpace(accessToken)) return;
        AvailabilitySwitch.IsEnabled = false;
        var location = await TryGetLocationAsync();
        var result = await apiClient.UpdateAvailabilityAsync(accessToken, new UpdateProviderMobileAvailabilityRequest(
            e.Value,
            location is null ? null : (decimal)location.Latitude,
            location is null ? null : (decimal)location.Longitude));
        if (!result.IsSuccess) ShowMessage(result.ErrorMessage ?? "Disponibilité non modifiée.");
        await LoadHomeAsync();
    }

    private async void OnAcceptClicked(object? sender, EventArgs e)
    {
        if (apiClient is null || currentLiveOffer is null || string.IsNullOrWhiteSpace(accessToken)) return;
        if (HasOfferExpired(currentLiveOffer.ExpiresAt))
        {
            await ExpireOfferAsync(currentLiveOffer.AssignmentId);
            return;
        }

        offerActionInProgress = true;
        SetBusy(true);
        var location = await TryGetLocationAsync();
        var result = await apiClient.AcceptMissionAsync(accessToken, currentLiveOffer.AssignmentId, new ProviderAcceptMissionRequest(
            location is null ? null : (decimal)location.Latitude,
            location is null ? null : (decimal)location.Longitude,
            location?.Accuracy is null ? null : (int)Math.Round(location.Accuracy.Value)));
        offerActionInProgress = false;
        SetBusy(false);
        if (!result.IsSuccess) ShowMessage(result.ErrorMessage ?? "Acceptation impossible.");
        else await Shell.Current.GoToAsync($"{nameof(MissionDetailPage)}?assignmentId={currentLiveOffer.AssignmentId:D}");
    }

    private async void OnRefuseClicked(object? sender, EventArgs e)
    {
        if (apiClient is null || currentLiveOffer is null || string.IsNullOrWhiteSpace(accessToken)) return;
        if (HasOfferExpired(currentLiveOffer.ExpiresAt))
        {
            await ExpireOfferAsync(currentLiveOffer.AssignmentId);
            return;
        }

        offerActionInProgress = true;
        SetBusy(true);
        var result = await apiClient.RefuseMissionAsync(accessToken, currentLiveOffer.AssignmentId, new ProviderRefuseMissionRequest("Unavailable", "Refus depuis l’application mobile."));
        offerActionInProgress = false;
        SetBusy(false);
        if (!result.IsSuccess) ShowMessage(result.ErrorMessage ?? "Refus impossible.");
        await LoadHomeAsync();
    }

    private async void OnUpcomingMissionClicked(object? sender, TappedEventArgs e)
    {
        if (currentUpcomingMission is not null) await Shell.Current.GoToAsync($"{nameof(MissionDetailPage)}?assignmentId={currentUpcomingMission.AssignmentId:D}");
        else await Shell.Current.GoToAsync("//missions");
    }

    private async void OnNotificationsClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(NotificationsPage));
    private async void OnMissionsClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//missions");

    private static async Task<Location?> TryGetLocationAsync()
    {
        try { return await Geolocation.Default.GetLastKnownLocationAsync(); }
        catch { return null; }
    }

    private void SetBusy(bool busy)
    {
        var offerIsActive = currentLiveOffer is not null && !HasOfferExpired(currentLiveOffer.ExpiresAt);
        AcceptButton.IsEnabled = !busy && offerIsActive;
        RefuseButton.IsEnabled = !busy && offerIsActive;
    }

    private void StartOfferCountdown(Guid assignmentId, DateTimeOffset expiresAt)
    {
        offerCountdownCancellation = new CancellationTokenSource();
        _ = RunOfferCountdownAsync(assignmentId, expiresAt, offerCountdownCancellation.Token);
    }

    private async Task RunOfferCountdownAsync(Guid assignmentId, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var expired = await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (currentLiveOffer?.AssignmentId != assignmentId) return false;
                    UpdateOfferCountdown(expiresAt);
                    return HasOfferExpired(expiresAt);
                });

                if (!expired) continue;
                await MainThread.InvokeOnMainThreadAsync(() => ExpireOfferAsync(assignmentId));
                return;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private void UpdateOfferCountdown(DateTimeOffset expiresAt)
    {
        var seconds = RemainingSeconds(expiresAt);
        LiveOfferCountdownLabel.Text = FormatSeconds(seconds);
        if (seconds == 0) SetBusy(true);
    }

    private async Task ExpireOfferAsync(Guid assignmentId)
    {
        if (currentLiveOffer?.AssignmentId != assignmentId || offerActionInProgress) return;
        LiveOfferCountdownLabel.Text = "00:00";
        SetBusy(true);
        await LoadHomeAsync();
    }

    private void StopOfferCountdown()
    {
        offerCountdownCancellation?.Cancel();
        offerCountdownCancellation?.Dispose();
        offerCountdownCancellation = null;
    }

    private void ShowMessage(string message)
    {
        MessageLabel.Text = message;
        MessageBanner.IsVisible = true;
    }

    private static string FirstName(string displayName) => displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? displayName;
    private static string FormatMissionTime(DateTimeOffset? value) => value?.LocalDateTime.ToString("ddd d MMM · HH:mm") ?? "Horaire à confirmer";
    private static int RemainingSeconds(DateTimeOffset expiresAt)
        => Math.Max(0, (int)Math.Ceiling((expiresAt - DateTimeOffset.UtcNow).TotalSeconds));
    private static bool HasOfferExpired(DateTimeOffset expiresAt) => expiresAt <= DateTimeOffset.UtcNow;
    private static string FormatSeconds(int seconds) => $"{Math.Max(0, seconds) / 60:00}:{Math.Max(0, seconds) % 60:00}";
    private static string FormatDistance(double? distanceKm) => distanceKm is null ? "distance à confirmer" : $"{distanceKm:0.0} km";

    private async Task ReturnToLoginAsync()
    {
        if (sessionService is not null) await sessionService.ClearAsync();
        if (Application.Current?.Windows.FirstOrDefault() is { } window) window.Page = new NavigationPage(new LoginPage());
    }
}
