using System.Collections.ObjectModel;

namespace HomeService.Client.Mobile.Pages;

public partial class OnboardingPage : ContentPage
{
    private const string OnboardingSeenKey = "ClientOnboardingSeen";

    public OnboardingPage()
    {
        InitializeComponent();
        Slides = new ObservableCollection<OnboardingSlide>
        {
            new("client_onboarding_1.png"),
            new("client_onboarding_2.png"),
            new("client_onboarding_3.png")
        };

        BindingContext = this;
    }

    public ObservableCollection<OnboardingSlide> Slides { get; }

    private async void OnNextClicked(object sender, EventArgs e)
    {
        if (SlidesView.Position < Slides.Count - 1)
        {
            SlidesView.Position += 1;
            return;
        }

        Preferences.Default.Set(OnboardingSeenKey, true);
        await Shell.Current.GoToAsync("//welcome");
    }

    private void OnPositionChanged(object sender, PositionChangedEventArgs e)
    {
        NextButton.Text = e.CurrentPosition >= Slides.Count - 1 ? "Commencer" : "Suivant";
    }

    public sealed record OnboardingSlide(string ImageSource);
}
