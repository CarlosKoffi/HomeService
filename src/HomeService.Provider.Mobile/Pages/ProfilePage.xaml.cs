using HomeService.Contracts.ProviderPortal;
using HomeService.Provider.Mobile.Services;
using Microsoft.Maui.Devices.Sensors;

namespace HomeService.Provider.Mobile.Pages;

public partial class ProfilePage : ContentPage
{
    private readonly ProviderMobileApiClient? apiClient;
    private readonly ProviderSessionService? sessionService;
    private string? accessToken;
    private bool renderingAvailability;

    public ProfilePage()
    {
        InitializeComponent();
        apiClient = IPlatformApplication.Current?.Services.GetService<ProviderMobileApiClient>();
        sessionService = IPlatformApplication.Current?.Services.GetService<ProviderSessionService>();
    }

    protected override async void OnAppearing() { base.OnAppearing(); await LoadProfileAsync(); }
    private async void OnRefreshing(object? sender, EventArgs e) { await LoadProfileAsync(); RefreshHost.IsRefreshing = false; }

    private async Task LoadProfileAsync()
    {
        accessToken = sessionService is null ? null : await sessionService.GetAccessTokenAsync();
        MessageBanner.IsVisible = false;
        if (string.IsNullOrWhiteSpace(accessToken) || apiClient is null)
        {
            ShowMessage("Votre session a expiré. Reconnectez-vous pour consulter le profil.");
            return;
        }
        var result = await apiClient.GetProfileAsync(accessToken);
        if (!result.IsSuccess || result.Response is null)
        {
            ShowMessage(result.ErrorMessage ?? "Impossible de charger le profil depuis l’API.");
            return;
        }

        var profile = result.Response;
        FullNameLabel.Text = profile.FullName;
        EmploymentTypeLabel.Text = profile.EmploymentType == "TemporaryWorker" ? "Intérimaire" : "Salarié d’entreprise";
        CompanyLabel.Text = profile.CompanyName;
        var percent = profile.ProfileCompletion?.Percent ?? 100;
        CompletionLabel.Text = $"{percent}%";
        CompletionProgress.Progress = percent / 100d;
        CompletionMessageLabel.Text = profile.ProfileCompletion?.Message ?? "Votre profil professionnel est complet.";

        var homeResult = await apiClient.GetHomeResultAsync(accessToken);
        renderingAvailability = true;
        try
        {
            if (homeResult.IsSuccess && homeResult.Response is not null)
            {
                var status = homeResult.Response.Status;
                AvailabilitySwitch.IsToggled = status.IsAvailable;
                AvailabilitySwitch.IsEnabled = status.CanChangeAvailability;
                AvailabilityLabel.Text = status.AvailabilityLabel;
                AvailabilityLabel.TextColor = Color.FromArgb(status.IsAvailable ? "#16B364" : "#DC2626");
                AvailabilityMessageLabel.Text = status.AvailabilityMessage;
            }
            else
            {
                AvailabilitySwitch.IsToggled = profile.IsAvailable;
                AvailabilitySwitch.IsEnabled = false;
                AvailabilityLabel.Text = profile.IsAvailable ? "Disponible" : "Indisponible";
                AvailabilityLabel.TextColor = Color.FromArgb(profile.IsAvailable ? "#16B364" : "#DC2626");
                AvailabilityMessageLabel.Text = "Actualisez le profil pour modifier votre disponibilité.";
            }
        }
        finally
        {
            renderingAvailability = false;
        }

        if (!string.IsNullOrWhiteSpace(profile.ProfilePhotoUrl))
        {
            var photo = await apiClient.DownloadAsync(accessToken, profile.ProfilePhotoUrl);
            if (photo.Response is { Length: > 0 } bytes)
            {
                ProfileImage.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
            }
        }
    }

    private async void OnDetailsClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(ProfileDetailsPage));
    private async void OnDocumentsClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(DocumentsPage));
    private async void OnServicesClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(ServicesPage));
    private async void OnPortfolioClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(PortfolioPage));
    private async void OnSettingsClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(SettingsPage));
    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//home");
    private async void OnPhotoTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync(nameof(ProfileDetailsPage));
    private async void OnDetailsTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync(nameof(ProfileDetailsPage));
    private async void OnDocumentsTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync(nameof(DocumentsPage));
    private async void OnServicesTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync(nameof(ServicesPage));
    private async void OnPortfolioTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync(nameof(PortfolioPage));
    private async void OnSettingsTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync(nameof(SettingsPage));

    private async void OnAvailabilityToggled(object? sender, ToggledEventArgs e)
    {
        if (renderingAvailability || apiClient is null || string.IsNullOrWhiteSpace(accessToken)) return;

        AvailabilitySwitch.IsEnabled = false;
        var location = await TryGetLocationAsync();
        var result = await apiClient.UpdateAvailabilityAsync(accessToken, new UpdateProviderMobileAvailabilityRequest(
            e.Value,
            location is null ? null : (decimal)location.Latitude,
            location is null ? null : (decimal)location.Longitude));

        if (!result.IsSuccess)
        {
            var errorMessage = result.ErrorMessage ?? "Disponibilité non modifiée.";
            await LoadProfileAsync();
            ShowMessage(errorMessage);
            return;
        }

        await LoadProfileAsync();
    }

    private static async Task<Location?> TryGetLocationAsync()
    {
        try { return await Geolocation.Default.GetLastKnownLocationAsync(); }
        catch { return null; }
    }

    private void ShowMessage(string message) { MessageLabel.Text = message; MessageBanner.IsVisible = true; }
}
