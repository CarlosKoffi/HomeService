namespace HomeService.Client.Mobile.Pages;

public partial class OnboardingPage : ContentPage
{
    private const string OnboardingSeenKey = "ClientOnboardingSeen";
    private static readonly Color ActiveDotColor = Color.FromArgb("#155EEF");
    private static readonly Color InactiveDotColor = Color.FromArgb("#DCE7FF");

    private readonly OnboardingSlide[] slides =
    [
        new(
            "Bienvenue\ndans Wélé 👋",
            "Tous vos services,\nen quelques clics",
            "Plomberie, électricité, ménage,\nbeauté, et bien plus encore.",
            "client_onboarding_1.png",
            -405,
            280,
            true),
        new(
            "Des professionnels\nvérifiés et notés",
            string.Empty,
            "Des experts de confiance,\névalués par nos clients\ncomme vous.",
            "client_onboarding_2.png",
            -292,
            310,
            false),
        new(
            "Suivi transparent\nen temps réel",
            string.Empty,
            "Suivez chaque étape de votre\ndemande jusqu’à la fin\nde l’intervention.",
            "client_onboarding_3.png",
            -282,
            350,
            false)
    ];

    private int currentIndex;

    public OnboardingPage()
    {
        InitializeComponent();
        RenderCurrentSlide();
    }

    private async void OnNextClicked(object sender, EventArgs e)
    {
        if (currentIndex < slides.Length - 1)
        {
            currentIndex++;
            RenderCurrentSlide();
            return;
        }

        Preferences.Default.Set(OnboardingSeenKey, true);
        await Shell.Current.GoToAsync("//welcome");
    }

    private void RenderCurrentSlide()
    {
        var slide = slides[currentIndex];
        BrandLabel.IsVisible = slide.ShowBrand;
        TitleLabel.Text = slide.Title;
        LeadLabel.Text = slide.Lead;
        LeadLabel.IsVisible = !string.IsNullOrWhiteSpace(slide.Lead);
        BodyLabel.Text = slide.Body;
        IllustrationImage.Source = slide.ImageSource;
        IllustrationImage.TranslationY = slide.ImageOffsetY;
        IllustrationViewport.HeightRequest = slide.ViewportHeight;
        NextButton.Text = currentIndex == slides.Length - 1 ? "Commencer" : "Suivant";

        DotOne.Fill = currentIndex == 0 ? ActiveDotColor : InactiveDotColor;
        DotTwo.Fill = currentIndex == 1 ? ActiveDotColor : InactiveDotColor;
        DotThree.Fill = currentIndex == 2 ? ActiveDotColor : InactiveDotColor;
    }

    private sealed record OnboardingSlide(
        string Title,
        string Lead,
        string Body,
        string ImageSource,
        double ImageOffsetY,
        double ViewportHeight,
        bool ShowBrand);
}
