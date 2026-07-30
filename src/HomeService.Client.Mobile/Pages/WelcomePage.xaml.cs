namespace HomeService.Client.Mobile.Pages;

public partial class WelcomePage : ContentPage
{
    private readonly Services.ClientSessionStore sessionStore;

    public WelcomePage()
    {
        InitializeComponent();
        sessionStore = Services.MobileServiceLocator.GetRequiredService<Services.ClientSessionStore>();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(LoginPage));
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(RegisterPage));
    }

    private async void OnGoogleClicked(object sender, EventArgs e)
    {
        await DisplayAlert("Connexion Google", "La configuration Firebase est prête. L'authentification Google sera branchée dans le prochain lot.", "Compris");
    }

    private async void OnAppleClicked(object sender, EventArgs e)
    {
        await DisplayAlert("Connexion Apple", "Cette option sera activée dès que le compte Apple Developer sera disponible.", "Compris");
    }

    private async void OnPhoneClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(LoginPage));
    }

    private async void OnPreviewClicked(object sender, EventArgs e)
    {
        await sessionStore.StartPreviewAsync();
        await Shell.Current.GoToAsync("//home");
    }

    private async void OnReplayOnboardingClicked(object sender, EventArgs e)
    {
        Preferences.Default.Set("ClientOnboardingSeen", false);
        await Shell.Current.GoToAsync(nameof(OnboardingPage));
    }
}
