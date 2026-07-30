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
