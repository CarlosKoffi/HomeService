using HomeService.Contracts.ProviderPortal;
using HomeService.Provider.Mobile.Services;

namespace HomeService.Provider.Mobile.Pages;

public partial class ProfileDetailsPage : ContentPage
{
    private readonly ProviderMobileApiClient? apiClient;
    private readonly ProviderSessionService? sessionService;
    private string? accessToken;

    public ProfileDetailsPage()
    {
        InitializeComponent();
        apiClient = IPlatformApplication.Current?.Services.GetService<ProviderMobileApiClient>();
        sessionService = IPlatformApplication.Current?.Services.GetService<ProviderSessionService>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        accessToken = sessionService is null ? null : await sessionService.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(accessToken) || apiClient is null) return;
        var result = await apiClient.GetProfileAsync(accessToken);
        if (result.Response is null) return;
        var profile = result.Response;
        FirstNameEntry.Text = profile.FirstName;
        LastNameEntry.Text = profile.LastName;
        PhoneEntry.Text = profile.PhoneNumber;
        EmailEntry.Text = profile.Email;
        AddressEntry.Text = profile.Address;
        RadiusEntry.Text = profile.MissionRadiusKm.ToString();
        if (!string.IsNullOrWhiteSpace(profile.ProfilePhotoUrl))
        {
            var photo = await apiClient.DownloadAsync(accessToken, profile.ProfilePhotoUrl);
            if (photo.Response is { Length: > 0 } bytes) ProfileImage.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (apiClient is null || string.IsNullOrWhiteSpace(accessToken)) return;
        if (!int.TryParse(RadiusEntry.Text, out var radius)) radius = 5;
        SaveButton.IsEnabled = false;
        var result = await apiClient.UpdateProfileAsync(accessToken, new UpdateProviderMobileProfileRequest(
            FirstNameEntry.Text?.Trim() ?? string.Empty,
            LastNameEntry.Text?.Trim() ?? string.Empty,
            EmailEntry.Text?.Trim(),
            AddressEntry.Text?.Trim() ?? string.Empty,
            radius));
        SaveButton.IsEnabled = true;
        MessageLabel.Text = result.IsSuccess ? "Vos informations ont été enregistrées." : result.ErrorMessage ?? "Modification impossible.";
        MessageBanner.IsVisible = true;
    }

    private async void OnEditPhotoClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(DocumentsPage));
    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
