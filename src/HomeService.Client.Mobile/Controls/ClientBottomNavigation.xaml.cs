namespace HomeService.Client.Mobile.Controls;

public partial class ClientBottomNavigation : ContentView
{
    private bool isNavigating;
    public static readonly BindableProperty ActiveTabProperty = BindableProperty.Create(
        nameof(ActiveTab), typeof(string), typeof(ClientBottomNavigation), "",
        propertyChanged: static (bindable, _, _) => ((ClientBottomNavigation)bindable).UpdateState());

    public static readonly BindableProperty ShowCreateProperty = BindableProperty.Create(
        nameof(ShowCreate), typeof(bool), typeof(ClientBottomNavigation), false,
        propertyChanged: static (bindable, _, value) => ((ClientBottomNavigation)bindable).CreateButton.IsVisible = (bool)value);

    public string ActiveTab { get => (string)GetValue(ActiveTabProperty); set => SetValue(ActiveTabProperty, value); }
    public bool ShowCreate { get => (bool)GetValue(ShowCreateProperty); set => SetValue(ShowCreateProperty, value); }

    public ClientBottomNavigation()
    {
        InitializeComponent();
        UpdateState();
    }

    private void UpdateState()
    {
        var muted = Color.FromArgb("#667085");
        var active = Color.FromArgb("#2563EB");
        HomeLabel.TextColor = ActiveTab == "home" ? active : muted;
        RequestsLabel.TextColor = ActiveTab == "requests" ? active : muted;
        MessagesLabel.TextColor = ActiveTab == "messages" ? active : muted;
        ProfileLabel.TextColor = ActiveTab == "profile" ? active : muted;
        HomeIcon.Source = ActiveTab == "home" ? "nav_home_active.svg" : "nav_home.svg";
        RequestsIcon.Source = ActiveTab == "requests" ? "nav_requests_active.svg" : "nav_requests.svg";
        MessagesIcon.Source = ActiveTab == "messages" ? "nav_messages_active.svg" : "nav_messages.svg";
        ProfileIcon.Source = ActiveTab == "profile" ? "nav_profile_active.svg" : "nav_profile.svg";
    }

    private async Task GoAsync(string route, string? targetTab = null)
    {
        if (isNavigating || (!string.IsNullOrWhiteSpace(targetTab) && ActiveTab == targetTab))
        {
            return;
        }

        isNavigating = true;
        try
        {
            await Shell.Current.GoToAsync(route);
        }
        finally
        {
            isNavigating = false;
        }
    }

    private async void OnHomeTapped(object sender, TappedEventArgs e) => await GoAsync("//home", "home");
    private async void OnRequestsTapped(object sender, TappedEventArgs e) => await GoAsync("//requests", "requests");
    private async void OnMessagesTapped(object sender, TappedEventArgs e) => await GoAsync("//messages", "messages");
    private async void OnProfileTapped(object sender, TappedEventArgs e) => await GoAsync("//profile", "profile");
    private async void OnCreateTapped(object sender, TappedEventArgs e) => await GoAsync(nameof(Pages.CreateRequestPage));
}
