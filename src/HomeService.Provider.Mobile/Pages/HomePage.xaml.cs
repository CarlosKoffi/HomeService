using HomeService.Contracts.ProviderPortal;
using HomeService.Provider.Mobile.Services;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Storage;

namespace HomeService.Provider.Mobile.Pages;

public partial class HomePage : ContentPage
{
    private const string AccessTokenPreferenceKey = "ProviderAccessToken";
    private readonly ProviderMobileApiClient? apiClient;
    private ProviderMobileMissionOfferResponse? currentLiveOffer;
    private string? accessToken;

    public HomePage()
    {
        InitializeComponent();
        apiClient = IPlatformApplication.Current?.Services.GetService<ProviderMobileApiClient>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadHomeAsync();
    }

    private async Task LoadHomeAsync()
    {
        if (apiClient is null)
        {
            ShowMessage("Configuration mobile incomplete. Client API introuvable.");
            return;
        }

        accessToken = Preferences.Default.Get(AccessTokenPreferenceKey, string.Empty);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            ShowMessage("Connectez-vous pour voir vos missions.");
            SetEmptyState();
            return;
        }

        SetBusy(true);
        var result = await apiClient.GetHomeResultAsync(accessToken);
        SetBusy(false);

        if (!result.IsSuccess || result.Response is null)
        {
            ShowMessage(result.ErrorMessage ?? "Impossible de charger vos missions.");
            SetEmptyState();
            return;
        }

        HideMessage();
        RenderHome(result.Response);
    }

    private void RenderHome(ProviderMobileHomeResponse home)
    {
        AvailabilityLabel.Text = home.Status.AvailabilityLabel;
        AvailabilityBadgeLabel.Text = home.Status.IsAvailable ? "ON" : "OFF";
        ProviderStatusLabel.Text = $"{home.Status.CompanyName} peut vous affecter dans un rayon de {home.Status.MissionRadiusKm} km.";

        ProfileCompletionBanner.IsVisible = home.ProfileCompletion is not null;
        if (home.ProfileCompletion is not null)
        {
            ProfilePercentLabel.Text = $"{home.ProfileCompletion.Percent}%";
            ProfileMessageLabel.Text = home.ProfileCompletion.Message;
        }

        if (home.UpcomingMission is null)
        {
            UpcomingMissionTitleLabel.Text = "Aucune mission planifiee";
            UpcomingMissionDetailLabel.Text = "Les prochains rendez-vous apparaitront ici.";
        }
        else
        {
            UpcomingMissionTitleLabel.Text = $"{home.UpcomingMission.ServiceName} - {FormatMissionTime(home.UpcomingMission.ScheduledFor)}";
            UpcomingMissionDetailLabel.Text = $"{home.UpcomingMission.LocationLabel} - {home.UpcomingMission.CompanyName}. {home.UpcomingMission.Status}.";
        }

        currentLiveOffer = home.LiveOffer;
        LiveOfferCard.IsVisible = currentLiveOffer is not null;
        if (currentLiveOffer is not null)
        {
            LiveOfferCompanyLabel.Text = $"MISSION PROPOSEE PAR {currentLiveOffer.CompanyName}".ToUpperInvariant();
            LiveOfferTitleLabel.Text = $"{home.Status.DisplayName}, une mission vous attend";
            LiveOfferCountdownLabel.Text = FormatSeconds(currentLiveOffer.SecondsToRespond);
            LiveOfferDetailLabel.Text = $"{currentLiveOffer.ServiceName} - {currentLiveOffer.LocationLabel} - {FormatDistance(currentLiveOffer.DistanceKm)}";
        }
    }

    private async void OnAcceptClicked(object? sender, EventArgs e)
    {
        if (apiClient is null || currentLiveOffer is null || string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        SetBusy(true);
        var location = await TryGetLocationAsync();
        var request = new ProviderAcceptMissionRequest(
            location?.Latitude is null ? null : (decimal)location.Latitude,
            location?.Longitude is null ? null : (decimal)location.Longitude,
            location?.Accuracy is null ? null : (int)Math.Round(location.Accuracy.Value));
        var result = await apiClient.AcceptMissionAsync(accessToken, currentLiveOffer.AssignmentId, request);
        SetBusy(false);

        if (!result.IsSuccess)
        {
            ShowMessage(result.ErrorMessage ?? "Acceptation impossible.");
            return;
        }

        ShowMessage("Mission acceptee. Le client sera informe.");
        await LoadHomeAsync();
    }

    private async void OnRefuseClicked(object? sender, EventArgs e)
    {
        if (apiClient is null || currentLiveOffer is null || string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        SetBusy(true);
        var result = await apiClient.RefuseMissionAsync(
            accessToken,
            currentLiveOffer.AssignmentId,
            new ProviderRefuseMissionRequest("Unavailable", "Refus depuis l'application mobile."));
        SetBusy(false);

        if (!result.IsSuccess)
        {
            ShowMessage(result.ErrorMessage ?? "Refus impossible.");
            return;
        }

        ShowMessage("Mission refusee. Elle ne vous sera plus reproposee.");
        await LoadHomeAsync();
    }

    private static async Task<Location?> TryGetLocationAsync()
    {
        try
        {
            return await Geolocation.Default.GetLastKnownLocationAsync()
                ?? await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(6)));
        }
        catch
        {
            return null;
        }
    }

    private void SetBusy(bool isBusy)
    {
        AcceptButton.IsEnabled = !isBusy;
        RefuseButton.IsEnabled = !isBusy;
    }

    private void SetEmptyState()
    {
        ProfileCompletionBanner.IsVisible = false;
        LiveOfferCard.IsVisible = false;
        UpcomingMissionTitleLabel.Text = "Aucune mission chargee";
        UpcomingMissionDetailLabel.Text = "Connectez-vous ou verifiez votre reseau.";
    }

    private void ShowMessage(string message)
    {
        MessageLabel.Text = message;
        MessageBanner.IsVisible = true;
    }

    private void HideMessage()
    {
        MessageBanner.IsVisible = false;
    }

    private static string FormatMissionTime(DateTimeOffset? scheduledFor)
    {
        return scheduledFor is null ? "horaire a confirmer" : scheduledFor.Value.LocalDateTime.ToString("HH:mm");
    }

    private static string FormatSeconds(int seconds)
    {
        seconds = Math.Max(0, seconds);
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }

    private static string FormatDistance(double? distanceKm)
    {
        return distanceKm is null ? "distance a confirmer" : $"{distanceKm:0.0} km";
    }
}
