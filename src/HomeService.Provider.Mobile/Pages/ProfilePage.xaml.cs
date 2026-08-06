using HomeService.Contracts.ProviderPortal;
using HomeService.Provider.Mobile.Services;

namespace HomeService.Provider.Mobile.Pages;

public partial class ProfilePage : ContentPage
{
    private readonly ProviderMobileApiClient? apiClient;
    private readonly ProviderSessionService? sessionService;

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
        var token = sessionService is null ? null : await sessionService.GetAccessTokenAsync();
        MessageBanner.IsVisible = false;
        if (string.IsNullOrWhiteSpace(token) || apiClient is null)
        {
            ShowMessage("Votre session a expiré. Reconnectez-vous pour consulter le profil.");
            return;
        }
        var result = await apiClient.GetProfileAsync(token);
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

        if (!string.IsNullOrWhiteSpace(profile.ProfilePhotoUrl))
        {
            var photo = await apiClient.DownloadAsync(token, profile.ProfilePhotoUrl);
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
    private async void OnPhotoTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync(nameof(ProfileDetailsPage));
    private async void OnDetailsTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync(nameof(ProfileDetailsPage));
    private async void OnDocumentsTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync(nameof(DocumentsPage));
    private async void OnServicesTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync(nameof(ServicesPage));
    private async void OnPortfolioTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync(nameof(PortfolioPage));
    private async void OnSettingsTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync(nameof(SettingsPage));
    private void ShowMessage(string message) { MessageLabel.Text = message; MessageBanner.IsVisible = true; }
}
