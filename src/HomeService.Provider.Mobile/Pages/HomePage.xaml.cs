using HomeService.Contracts.ProviderPortal;
using HomeService.Provider.Mobile.Services;
using Microsoft.Maui.Devices.Sensors;

namespace HomeService.Provider.Mobile.Pages;

public partial class HomePage : ContentPage
{
    private readonly ProviderMobileApiClient? apiClient;
    private readonly ProviderSessionService? sessionService;
    private ProviderMobileMissionOfferResponse? currentLiveOffer;
    private string? accessToken;
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
        var homeResult = await apiClient.GetHomeResultAsync(accessToken);
        var missionsResult = await apiClient.GetMissionsAsync(
            accessToken,
            DateTimeOffset.UtcNow.Date.AddDays(-1),
            DateTimeOffset.UtcNow.Date.AddDays(31));
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
        RenderHome(homeResult.Response, missionsResult.Response?.Items ?? []);
    }

    private void RenderHome(ProviderMobileHomeResponse home, IReadOnlyList<ProviderMobileMissionSummaryResponse> missions)
    {
        GreetingLabel.Text = $"Bonjour {FirstName(home.Status.DisplayName)} 👋";
        ProviderStatusLabel.Text = home.Status.CompanyName;
        renderingAvailability = true;
        AvailabilitySwitch.IsToggled = home.Status.IsAvailable;
        AvailabilitySwitch.IsEnabled = home.Status.CanChangeAvailability;
        renderingAvailability = false;
        AvailabilityLabel.Text = home.Status.AvailabilityLabel;
        AvailabilityLabel.TextColor = home.Status.IsAvailable ? Color.FromArgb("#16B364") : Color.FromArgb("#DC2626");
        AvailabilityInfoCard.IsVisible = !home.Status.CanChangeAvailability;
        AvailabilityMessageLabel.Text = home.Status.AvailabilityMessage;

        ProfileCompletionBanner.IsVisible = home.ProfileCompletion is not null;
        if (home.ProfileCompletion is not null)
        {
            ProfilePercentLabel.Text = $"{home.ProfileCompletion.Percent}%";
            ProfileMessageLabel.Text = home.ProfileCompletion.Message;
        }

        var today = DateTime.Today;
        TodayCountLabel.Text = missions.Count(item => item.ScheduledFor?.LocalDateTime.Date == today).ToString();
        ActiveCountLabel.Text = missions.Count(item => item.Status is "Accepted" or "Started").ToString();
        UpcomingCountLabel.Text = missions.Count(item => item.Status == "Accepted" && item.ScheduledFor?.LocalDateTime.Date > today).ToString();

        if (home.UpcomingMission is null)
        {
            UpcomingMissionTitleLabel.Text = "Aucune mission planifiée";
            UpcomingMissionDetailLabel.Text = "Les prochains rendez-vous apparaîtront ici.";
        }
        else
        {
            UpcomingMissionTitleLabel.Text = home.UpcomingMission.ServiceName;
            UpcomingMissionDetailLabel.Text = $"{FormatMissionTime(home.UpcomingMission.ScheduledFor)} · {home.UpcomingMission.LocationLabel}";
        }

        currentLiveOffer = home.LiveOffer;
        LiveOfferCard.IsVisible = currentLiveOffer is not null;
        if (currentLiveOffer is not null)
        {
            LiveOfferCompanyLabel.Text = currentLiveOffer.CompanyName.ToUpperInvariant();
            LiveOfferTitleLabel.Text = currentLiveOffer.ServiceName;
            LiveOfferCountdownLabel.Text = FormatSeconds(currentLiveOffer.SecondsToRespond);
            LiveOfferDetailLabel.Text = $"{currentLiveOffer.LocationLabel} · {FormatDistance(currentLiveOffer.DistanceKm)}";
        }
    }

    private async void OnAvailabilityToggled(object? sender, ToggledEventArgs e)
    {
        if (renderingAvailability || apiClient is null || string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        AvailabilitySwitch.IsEnabled = false;
        var location = await TryGetLocationAsync();
        var result = await apiClient.UpdateAvailabilityAsync(
            accessToken,
            new UpdateProviderMobileAvailabilityRequest(
                e.Value,
                location is null ? null : (decimal)location.Latitude,
                location is null ? null : (decimal)location.Longitude));
        if (!result.IsSuccess)
        {
            ShowMessage(result.ErrorMessage ?? "Disponibilité non modifiée.");
        }

        await LoadHomeAsync();
    }

    private async void OnAcceptClicked(object? sender, EventArgs e)
    {
        if (apiClient is null || currentLiveOffer is null || string.IsNullOrWhiteSpace(accessToken)) return;
        SetBusy(true);
        var location = await TryGetLocationAsync();
        var result = await apiClient.AcceptMissionAsync(accessToken, currentLiveOffer.AssignmentId, new ProviderAcceptMissionRequest(
            location is null ? null : (decimal)location.Latitude,
            location is null ? null : (decimal)location.Longitude,
            location?.Accuracy is null ? null : (int)Math.Round(location.Accuracy.Value)));
        SetBusy(false);
        if (!result.IsSuccess) ShowMessage(result.ErrorMessage ?? "Acceptation impossible.");
        await LoadHomeAsync();
    }

    private async void OnRefuseClicked(object? sender, EventArgs e)
    {
        if (apiClient is null || currentLiveOffer is null || string.IsNullOrWhiteSpace(accessToken)) return;
        SetBusy(true);
        var result = await apiClient.RefuseMissionAsync(accessToken, currentLiveOffer.AssignmentId, new ProviderRefuseMissionRequest("Unavailable", "Refus depuis l’application mobile."));
        SetBusy(false);
        if (!result.IsSuccess) ShowMessage(result.ErrorMessage ?? "Refus impossible.");
        await LoadHomeAsync();
    }

    private async void OnNotificationsClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(NotificationsPage));
    private async void OnMissionsClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//missions");
    private async void OnCalendarClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//calendar");
    private async void OnMessagesClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//messages");

    private static async Task<Location?> TryGetLocationAsync()
    {
        try { return await Geolocation.Default.GetLastKnownLocationAsync(); }
        catch { return null; }
    }

    private void SetBusy(bool busy) { AcceptButton.IsEnabled = !busy; RefuseButton.IsEnabled = !busy; }
    private void ShowMessage(string message) { MessageLabel.Text = message; MessageBanner.IsVisible = true; }
    private static string FirstName(string displayName) => displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? displayName;
    private static string FormatMissionTime(DateTimeOffset? value) => value?.LocalDateTime.ToString("ddd d MMM · HH:mm") ?? "Horaire à confirmer";
    private static string FormatSeconds(int seconds) => $"{Math.Max(0, seconds) / 60:00}:{Math.Max(0, seconds) % 60:00}";
    private static string FormatDistance(double? distanceKm) => distanceKm is null ? "distance à confirmer" : $"{distanceKm:0.0} km";

    private async Task ReturnToLoginAsync()
    {
        if (sessionService is not null) await sessionService.ClearAsync();
        if (Application.Current?.Windows.FirstOrDefault() is { } window) window.Page = new NavigationPage(new LoginPage());
    }
}
