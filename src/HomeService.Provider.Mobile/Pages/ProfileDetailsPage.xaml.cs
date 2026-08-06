using HomeService.Contracts.Clients;
using HomeService.Contracts.ProviderPortal;
using HomeService.Provider.Mobile.Services;

namespace HomeService.Provider.Mobile.Pages;

public partial class ProfileDetailsPage : ContentPage
{
    private readonly ProviderMobileApiClient? apiClient;
    private readonly ProviderSessionService? sessionService;
    private readonly ProviderAddressAutocompleteSession? addressAutocomplete;
    private string? accessToken;
    private decimal? missionLatitude;
    private decimal? missionLongitude;
    private string verifiedAddress = string.Empty;
    private bool applyingAddress;
    private bool isPageActive;

    public ProfileDetailsPage()
    {
        InitializeComponent();
        apiClient = IPlatformApplication.Current?.Services.GetService<ProviderMobileApiClient>();
        sessionService = IPlatformApplication.Current?.Services.GetService<ProviderSessionService>();
        if (apiClient is not null)
        {
            addressAutocomplete = new ProviderAddressAutocompleteSession(apiClient);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        isPageActive = true;
        accessToken = sessionService is null ? null : await sessionService.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(accessToken) || apiClient is null) return;
        var result = await apiClient.GetProfileAsync(accessToken);
        if (result.Response is null) return;
        var profile = result.Response;
        applyingAddress = true;
        FirstNameEntry.Text = profile.FirstName;
        LastNameEntry.Text = profile.LastName;
        PhoneEntry.Text = profile.PhoneNumber;
        EmailEntry.Text = profile.Email;
        AddressEntry.Text = profile.Address;
        applyingAddress = false;
        verifiedAddress = profile.Address.Trim();
        missionLatitude = profile.MissionLatitude;
        missionLongitude = profile.MissionLongitude;
        UpdateAddressVerificationState();
        RadiusEntry.Text = profile.MissionRadiusKm.ToString();
        if (!string.IsNullOrWhiteSpace(profile.ProfilePhotoUrl))
        {
            var photo = await apiClient.DownloadAsync(accessToken, profile.ProfilePhotoUrl);
            if (photo.Response is { Length: > 0 } bytes) ProfileImage.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
        }
    }

    protected override void OnDisappearing()
    {
        isPageActive = false;
        addressAutocomplete?.CancelPendingSearch();
        SuggestionsPanel.IsVisible = false;
        base.OnDisappearing();
    }

    private async void OnAddressTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (applyingAddress || !isPageActive || addressAutocomplete is null || string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        var value = e.NewTextValue?.Trim() ?? string.Empty;
        if (string.Equals(value, verifiedAddress, StringComparison.Ordinal))
        {
            return;
        }

        missionLatitude = null;
        missionLongitude = null;
        VerifiedAddressRow.IsVisible = false;
        AddressErrorLabel.IsVisible = false;
        SuggestionsPanel.IsVisible = false;

        var result = await addressAutocomplete.SearchAsync(accessToken, value);
        if (result.IsIgnored || !isPageActive || applyingAddress)
        {
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (!isPageActive || applyingAddress)
            {
                return;
            }

            SuggestionsView.ItemsSource = result.Suggestions;
            SuggestionsPanel.IsVisible = result.Suggestions.Count > 0;
            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                ShowAddressError(result.ErrorMessage);
            }
        });
    }

    private async void OnSuggestionSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ClientAddressSuggestionResponse suggestion
            || addressAutocomplete is null
            || string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        SuggestionsView.SelectedItem = null;
        SuggestionsPanel.IsVisible = false;
        AddressErrorLabel.IsVisible = false;
        var details = await addressAutocomplete.ResolveAsync(accessToken, suggestion);
        if (!isPageActive)
        {
            return;
        }

        if (details is null)
        {
            ShowAddressError("Cette adresse n’a pas pu être vérifiée. Réessayez en sélectionnant une proposition Google.");
            return;
        }

        applyingAddress = true;
        AddressEntry.Text = details.AddressLine;
        applyingAddress = false;
        verifiedAddress = details.AddressLine.Trim();
        missionLatitude = details.Latitude;
        missionLongitude = details.Longitude;
        UpdateAddressVerificationState();
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (apiClient is null || string.IsNullOrWhiteSpace(accessToken)) return;
        var address = AddressEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(address) || missionLatitude is null || missionLongitude is null)
        {
            ShowAddressError("Recherchez puis sélectionnez votre adresse dans les propositions Google.");
            AddressEntry.Focus();
            return;
        }

        if (!int.TryParse(RadiusEntry.Text, out var radius)) radius = 5;
        SaveButton.IsEnabled = false;
        var result = await apiClient.UpdateProfileAsync(accessToken, new UpdateProviderMobileProfileRequest(
            FirstNameEntry.Text?.Trim() ?? string.Empty,
            LastNameEntry.Text?.Trim() ?? string.Empty,
            EmailEntry.Text?.Trim(),
            address,
            radius,
            missionLatitude,
            missionLongitude));
        SaveButton.IsEnabled = true;
        MessageLabel.Text = result.IsSuccess ? "Vos informations ont été enregistrées." : result.ErrorMessage ?? "Modification impossible.";
        MessageBanner.IsVisible = true;
        if (result.IsSuccess && result.Response is not null)
        {
            verifiedAddress = result.Response.Address.Trim();
            missionLatitude = result.Response.MissionLatitude;
            missionLongitude = result.Response.MissionLongitude;
            UpdateAddressVerificationState();
        }
    }

    private void UpdateAddressVerificationState()
    {
        VerifiedAddressRow.IsVisible = missionLatitude is not null && missionLongitude is not null;
        AddressErrorLabel.IsVisible = false;
    }

    private void ShowAddressError(string message)
    {
        AddressErrorLabel.Text = message;
        AddressErrorLabel.IsVisible = true;
        VerifiedAddressRow.IsVisible = false;
    }

    private async void OnEditPhotoClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(DocumentsPage));
    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
