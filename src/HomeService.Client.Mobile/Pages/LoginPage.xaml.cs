using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

public partial class LoginPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient;
    private readonly ClientSessionStore sessionStore;
    private readonly ClientDeviceRegistrationService deviceRegistrationService;

    public LoginPage()
    {
        InitializeComponent();
        apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
        sessionStore = MobileServiceLocator.GetRequiredService<ClientSessionStore>();
        deviceRegistrationService = MobileServiceLocator.GetRequiredService<ClientDeviceRegistrationService>();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        var request = new LoginClientRequest(
            PhoneEntry.Text?.Trim() ?? string.Empty,
            PasswordEntry.Text ?? string.Empty,
            RememberCheckBox.IsChecked);

        var result = await apiClient.LoginAsync(request);
        if (!result.IsSuccess || result.Response is null)
        {
            ShowError(result.ErrorMessage);
            return;
        }

        await sessionStore.SaveAsync(result.Response);
        await deviceRegistrationService.RegisterCurrentDeviceAsync();
        await Shell.Current.GoToAsync("//home");
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(RegisterPage));
    }

    private void ShowError(string? message)
    {
        ErrorLabel.Text = message ?? "Connexion impossible.";
        ErrorLabel.IsVisible = true;
    }
}
