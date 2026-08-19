using HomeService.Contracts.ProviderPortal;
using HomeService.Provider.Mobile.Services;

namespace HomeService.Provider.Mobile.Pages;

public partial class LoginPage : ContentPage
{
    private readonly ProviderMobileApiClient? apiClient;
    private readonly ProviderSessionService? sessionService;
    private bool sessionChecked;

    public LoginPage() : this(null)
    {
    }

    public LoginPage(string? phoneNumber)
    {
        InitializeComponent();
        apiClient = IPlatformApplication.Current?.Services.GetService<ProviderMobileApiClient>();
        sessionService = IPlatformApplication.Current?.Services.GetService<ProviderSessionService>();
        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            PhoneEntry.Text = phoneNumber.Trim();
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (sessionChecked)
        {
            return;
        }

        sessionChecked = true;
        var token = sessionService is null ? null : await sessionService.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token) || apiClient is null)
        {
            return;
        }

        SetBusy(true);
        var me = await apiClient.GetMeAsync(token);
        SetBusy(false);
        if (me.IsSuccess)
        {
            OpenApplication();
        }
        else
        {
            await sessionService!.ClearAsync();
        }
    }

    private async void OnLoginClicked(object? sender, EventArgs e) => await LoginAsync();

    private async void OnLoginCompleted(object? sender, EventArgs e) => await LoginAsync();

    private async Task LoginAsync()
    {
        var phone = PhoneEntry.Text?.Trim();
        var password = PasswordEntry.Text;
        if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(password))
        {
            ShowMessage("Renseignez votre téléphone et votre mot de passe.");
            return;
        }

        if (apiClient is null || sessionService is null)
        {
            ShowMessage("Configuration de l’application incomplète.");
            return;
        }

        SetBusy(true);
        var result = await apiClient.LoginAsync(new ProviderPortalLoginRequest(phone, password, true));
        SetBusy(false);
        if (!result.IsSuccess || result.Response is null)
        {
            ShowMessage(result.ErrorMessage ?? "Connexion impossible.");
            return;
        }

        await sessionService.SaveAsync(result.Response.AccessToken, result.Response.ExpiresAt, result.Response.DisplayName);
        try
        {
            var registration = IPlatformApplication.Current?.Services.GetService<ProviderDeviceRegistrationService>();
            if (registration is not null)
            {
                await registration.RegisterCurrentDeviceAsync();
            }
        }
        catch
        {
            // The token is retried when the application returns to the foreground.
        }
        OpenApplication();
    }

    private static void OpenApplication()
    {
        if (Application.Current?.Windows.FirstOrDefault() is { } window)
        {
            window.Page = new AppShell();
        }
    }

    private void SetBusy(bool busy)
    {
        LoginButton.IsEnabled = !busy;
        PhoneEntry.IsEnabled = !busy;
        PasswordEntry.IsEnabled = !busy;
        LoadingIndicator.IsVisible = busy;
        LoadingIndicator.IsRunning = busy;
    }

    private void ShowMessage(string message)
    {
        MessageLabel.Text = message;
        MessageBanner.IsVisible = true;
    }
}
