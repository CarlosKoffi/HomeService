using HomeService.Company.Mobile.Services;
using HomeService.Contracts.CompanyPortal;

namespace HomeService.Company.Mobile;

public partial class MainPage : ContentPage
{
    private readonly CompanyMobileApiClient apiClient;
    private readonly CompanySessionStore sessionStore;
    private bool checkedExistingSession;

    public MainPage()
    {
        InitializeComponent();
        apiClient = IPlatformApplication.Current!.Services.GetRequiredService<CompanyMobileApiClient>();
        sessionStore = IPlatformApplication.Current.Services.GetRequiredService<CompanySessionStore>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!checkedExistingSession)
        {
            checkedExistingSession = true;
            if (await sessionStore.HasSessionAsync())
            {
                App.ShowCompanyShell();
            }
        }
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        var email = EmailEntry.Text?.Trim() ?? string.Empty;
        var password = PasswordEntry.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ShowError("Saisissez votre email et votre mot de passe.");
            return;
        }

        SetLoading(true);
        var result = await apiClient.LoginAsync(new CompanyPortalLoginRequest(email, password, RememberSwitch.IsToggled));
        SetLoading(false);
        if (!result.IsSuccess || result.Response is null)
        {
            ShowError(result.ErrorMessage ?? "Connexion impossible.");
            return;
        }

        await sessionStore.SaveAsync(result.Response);
        App.ShowCompanyShell();
        var services = IPlatformApplication.Current?.Services;
        if (services is not null)
        {
            _ = services
                .GetRequiredService<CompanyDeviceRegistrationService>()
                .RegisterCurrentDeviceAsync();
        }
    }

    private void SetLoading(bool loading)
    {
        LoginButton.IsEnabled = !loading;
        LoadingIndicator.IsVisible = loading;
        LoadingIndicator.IsRunning = loading;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
