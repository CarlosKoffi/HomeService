using HomeService.Contracts.ProviderPortal;
using HomeService.Provider.Mobile.Services;

namespace HomeService.Provider.Mobile.Pages;

public partial class ProviderActivationPage : ContentPage
{
    private readonly string invitationCode;
    private readonly ProviderMobileApiClient? apiClient;
    private readonly ProviderSessionService? sessionService;
    private string? providerPhone;
    private bool invitationLoaded;

    public ProviderActivationPage(string invitationCode)
    {
        InitializeComponent();
        this.invitationCode = invitationCode.Trim();
        CodeEntry.Text = this.invitationCode;
        apiClient = IPlatformApplication.Current?.Services.GetService<ProviderMobileApiClient>();
        sessionService = IPlatformApplication.Current?.Services.GetService<ProviderSessionService>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (invitationLoaded)
        {
            return;
        }

        invitationLoaded = true;
        await LoadInvitationAsync();
    }

    private async Task LoadInvitationAsync()
    {
        if (apiClient is null)
        {
            ShowMessage("Configuration de l’application incomplète.");
            return;
        }

        SetBusy(true);
        var result = await apiClient.GetInvitationAsync(invitationCode);
        SetBusy(false);
        if (!result.IsSuccess || result.Response is null)
        {
            ShowMessage(result.ErrorMessage ?? "Cette invitation ne peut pas être chargée.");
            return;
        }

        providerPhone = result.Response.PhoneNumber;
        ProviderNameLabel.Text = result.Response.ProviderName;
        CompanyNameLabel.Text = result.Response.CompanyName;
        PhoneLabel.Text = result.Response.PhoneNumber;
        ActivateButton.IsEnabled = true;
    }

    private async void OnActivateClicked(object? sender, EventArgs e) => await ActivateAsync();

    private async void OnActivationCompleted(object? sender, EventArgs e) => await ActivateAsync();

    private async Task ActivateAsync()
    {
        var password = PasswordEntry.Text ?? string.Empty;
        var confirmation = ConfirmPasswordEntry.Text ?? string.Empty;
        if (password.Length < 8)
        {
            ShowMessage("Le mot de passe doit contenir au moins 8 caractères.");
            return;
        }

        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            ShowMessage("Les deux mots de passe ne correspondent pas.");
            return;
        }

        if (apiClient is null)
        {
            ShowMessage("Configuration de l’application incomplète.");
            return;
        }

        SetBusy(true);
        var activation = await apiClient.ActivateAsync(
            new ProviderInvitationActivationRequest(invitationCode, password, confirmation, true));

        if (!activation.IsSuccess || activation.Response is null)
        {
            SetBusy(false);
            ShowMessage(activation.ErrorMessage ?? "L’activation n’a pas pu être terminée.");
            return;
        }

        providerPhone = activation.Response.PhoneNumber;
        var login = await apiClient.LoginAsync(new ProviderPortalLoginRequest(providerPhone, password, true));
        if (login.IsSuccess && login.Response is not null && sessionService is not null)
        {
            await sessionService.SaveAsync(login.Response.AccessToken, login.Response.ExpiresAt, login.Response.DisplayName);
            await TryRegisterDeviceAsync();
            SetBusy(false);
            OpenApplication();
            return;
        }

        SetBusy(false);
        ActivationForm.IsVisible = false;
        MessageBanner.IsVisible = false;
        SuccessPanel.IsVisible = true;
    }

    private static async Task TryRegisterDeviceAsync()
    {
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
            // Device registration is retried when the application returns to the foreground.
        }
    }

    private void OnContinueClicked(object? sender, EventArgs e)
    {
        OpenLogin();
    }

    private void OnOpenLoginClicked(object? sender, EventArgs e)
    {
        OpenLogin();
    }

    private void OpenLogin()
    {
        if (Application.Current?.Windows.FirstOrDefault() is { } window)
        {
            window.Page = ProviderDeepLinkNavigationService.Wrap(new LoginPage(providerPhone));
        }
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
        ActivateButton.IsEnabled = !busy && providerPhone is not null;
        PasswordEntry.IsEnabled = !busy;
        ConfirmPasswordEntry.IsEnabled = !busy;
        LoadingIndicator.IsVisible = busy;
        LoadingIndicator.IsRunning = busy;
    }

    private void ShowMessage(string message)
    {
        MessageLabel.Text = message;
        MessageBanner.IsVisible = true;
    }
}
