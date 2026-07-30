using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

public partial class RegisterPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient;
    private readonly ClientSessionStore sessionStore;

    public RegisterPage()
    {
        InitializeComponent();
        apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
        sessionStore = MobileServiceLocator.GetRequiredService<ClientSessionStore>();
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        if (PasswordEntry.Text != ConfirmPasswordEntry.Text)
        {
            ShowError("Les deux mots de passe doivent etre identiques.");
            return;
        }

        var request = new RegisterClientRequest(
            FirstNameEntry.Text?.Trim() ?? string.Empty,
            LastNameEntry.Text?.Trim() ?? string.Empty,
            PhoneEntry.Text?.Trim() ?? string.Empty,
            string.IsNullOrWhiteSpace(EmailEntry.Text) ? null : EmailEntry.Text.Trim(),
            PasswordEntry.Text ?? string.Empty);

        var result = await apiClient.RegisterAsync(request);
        if (!result.IsSuccess || result.Response is null)
        {
            ShowError(result.ErrorMessage);
            return;
        }

        await sessionStore.SaveAsync(result.Response);
        await Shell.Current.GoToAsync("//home");
    }

    private void ShowError(string? message)
    {
        ErrorLabel.Text = message ?? "Creation impossible.";
        ErrorLabel.IsVisible = true;
    }
}
