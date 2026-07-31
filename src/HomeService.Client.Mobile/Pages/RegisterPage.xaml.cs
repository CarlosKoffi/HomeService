using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

public partial class RegisterPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient;
    private readonly ClientSessionStore sessionStore;
    private readonly ClientDeviceRegistrationService deviceRegistrationService;

    public RegisterPage()
    {
        InitializeComponent();
        apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
        sessionStore = MobileServiceLocator.GetRequiredService<ClientSessionStore>();
        deviceRegistrationService = MobileServiceLocator.GetRequiredService<ClientDeviceRegistrationService>();
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        ResetValidation();
        ErrorLabel.IsVisible = false;
        if (!TermsCheckBox.IsChecked)
        {
            ShowError("Vous devez accepter les conditions d'utilisation.");
            return;
        }

        var nameParts = (FullNameEntry.Text ?? string.Empty)
            .Trim()
            .Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (nameParts.Length < 2)
        {
            FullNameBorder.Stroke = Colors.Red;
            ShowError("Saisissez votre prenom et votre nom.");
            return;
        }

        var firstName = nameParts.ElementAtOrDefault(0) ?? string.Empty;
        var lastName = nameParts.ElementAtOrDefault(1) ?? string.Empty;

        var request = new RegisterClientRequest(
            firstName,
            lastName,
            PhoneEntry.Text?.Trim() ?? string.Empty,
            string.IsNullOrWhiteSpace(EmailEntry.Text) ? null : EmailEntry.Text.Trim(),
            PasswordEntry.Text ?? string.Empty);

        var result = await apiClient.RegisterAsync(request);
        if (!result.IsSuccess || result.Response is null)
        {
            HighlightInvalidField(result.ErrorMessage);
            ShowError(result.ErrorMessage);
            return;
        }

        await sessionStore.SaveAsync(result.Response);
        try
        {
            await deviceRegistrationService.RegisterCurrentDeviceAsync();
        }
        catch
        {
            // Push registration must never prevent a successful account creation.
        }
        await Shell.Current.GoToAsync("//home");
    }

    private void OnTogglePasswordClicked(object sender, EventArgs e)
    {
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
    }

    private void ShowError(string? message)
    {
        ErrorLabel.Text = message ?? "Creation impossible.";
        ErrorLabel.IsVisible = true;
    }

    private void ResetValidation()
    {
        var defaultStroke = Color.FromArgb("#DCE1E8");
        FullNameBorder.Stroke = defaultStroke;
        PhoneBorder.Stroke = defaultStroke;
        EmailBorder.Stroke = defaultStroke;
        PasswordBorder.Stroke = defaultStroke;
    }

    private void HighlightInvalidField(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var normalized = message.ToLowerInvariant();
        if (normalized.Contains("prenom") || normalized.Contains("nom "))
        {
            FullNameBorder.Stroke = Colors.Red;
        }

        if (normalized.Contains("numero") || normalized.Contains("telephone"))
        {
            PhoneBorder.Stroke = Colors.Red;
        }

        if (normalized.Contains("email"))
        {
            EmailBorder.Stroke = Colors.Red;
        }

        if (normalized.Contains("mot de passe"))
        {
            PasswordBorder.Stroke = Colors.Red;
        }
    }
}
