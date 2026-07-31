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
            "client_onboarding_1_v2.png",
            235,
            true),
        new(
            "Des professionnels\nvérifiés et notés",
            string.Empty,
            "Des experts de confiance,\névalués par nos clients\ncomme vous.",
            "client_onboarding_2_v2.png",
            265,
            false),
        new(
            "Suivi transparent\nen temps réel",
            string.Empty,
            "Suivez chaque étape de votre\ndemande jusqu’à la fin\nde l’intervention.",
            "client_onboarding_3_v2.png",
            270,
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
            await MoveToSlideAsync(currentIndex + 1);
            return;
        }

        Preferences.Default.Set(OnboardingSeenKey, true);
        await Shell.Current.GoToAsync("//welcome");
    }

    private async void OnSwiped(object sender, SwipedEventArgs e)
    {
        var targetIndex = e.Direction switch
        {
            SwipeDirection.Left => Math.Min(currentIndex + 1, slides.Length - 1),
            SwipeDirection.Right => Math.Max(currentIndex - 1, 0),
            _ => currentIndex
        };

        await MoveToSlideAsync(targetIndex);
    }

    private async Task MoveToSlideAsync(int targetIndex)
    {
        if (targetIndex == currentIndex)
        {
            return;
        }

        await IllustrationImage.FadeTo(0, 90);
        currentIndex = targetIndex;
        RenderCurrentSlide();
        await IllustrationImage.FadeTo(1, 140);
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
        double ViewportHeight,
        bool ShowBrand);
}
