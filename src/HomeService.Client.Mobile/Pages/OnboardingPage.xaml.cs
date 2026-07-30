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
            new(
                "Bienvenue dans Wélé",
                "Tous vos services, en quelques clics.",
                "Plomberie, électricité, ménage, beauté et bien plus encore.",
                "\uD83D\uDC4B"),
            new(
                "Des professionnels vérifiés et notés",
                "Des experts de confiance, évalués par nos clients comme vous.",
                "Choisissez avec plus de sérénité.",
                "\uD83D\uDEE1"),
            new(
                "Suivi transparent en temps réel",
                "Suivez chaque étape de votre demande jusqu'à la fin de l'intervention.",
                "Mission, messages et facture restent au même endroit.",
                "\uD83D\uDCCD")
        };

        BindingContext = this;
        SlidesIndicator.ItemsSource = Slides;
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
        await Shell.Current.GoToAsync(nameof(WelcomePage));
    }

    private void OnPositionChanged(object sender, PositionChangedEventArgs e)
    {
        NextButton.Text = e.CurrentPosition >= Slides.Count - 1 ? "Commencer" : "Suivant";
    }

    public sealed record OnboardingSlide(string Title, string Description, string Badge, string Icon);
}
