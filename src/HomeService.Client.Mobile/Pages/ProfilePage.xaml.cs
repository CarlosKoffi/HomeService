using HomeService.Client.Mobile.Services;

namespace HomeService.Client.Mobile.Pages;

public partial class ProfilePage : ContentPage
{
    private readonly ClientMobileApiClient apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
    private readonly ClientSessionStore sessionStore = MobileServiceLocator.GetRequiredService<ClientSessionStore>();
    public ProfilePage() => InitializeComponent();
    protected override async void OnAppearing() { base.OnAppearing(); await LoadAsync(); }
    private async Task LoadAsync()
    {
        LoggedOutCard.IsVisible = !sessionStore.HasSession(); LoggedInSection.IsVisible = sessionStore.HasSession(); if (!sessionStore.HasSession()) return;
        if (sessionStore.IsPreviewMode()) { SetIdentity("Carlos", "Konan", "+225 07 00 00 00 00", "carlos@wele.ci"); return; }
        var result = await apiClient.GetMeAsync(); if (result.IsSuccess && result.Response is not null) SetIdentity(result.Response.FirstName, result.Response.LastName, result.Response.PhoneNumber, result.Response.Email);
    }
    private void SetIdentity(string firstName, string lastName, string phone, string? email) { NameLabel.Text = $"{firstName} {lastName}"; InitialsLabel.Text = $"{firstName.FirstOrDefault()}{lastName.FirstOrDefault()}".ToUpperInvariant(); PhoneLabel.Text = phone; }
    private async void OnLoginClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(LoginPage));
    private async void OnRegisterClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(RegisterPage));
    private async void OnInformationClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(ProfileInformationPage));
    private async void OnAddressesClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(AddressesPage));
    private async void OnPaymentMethodsClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(PaymentMethodsPage));
    private async void OnRequestsClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("//requests");
    private async void OnReviewsClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(ReviewsPage));
    private async void OnNotificationsClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(ClientNotificationsPage));
    private async void OnSettingsClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(ClientSettingsPage));
}
