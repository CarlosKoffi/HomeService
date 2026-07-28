using HomeService.Contracts.ProviderPortal;
using HomeService.Provider.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace HomeService.Provider.Mobile.Pages;

public partial class ProfilePage : ContentPage
{
    private const string AccessTokenPreferenceKey = "ProviderAccessToken";
    private readonly ProviderMobileApiClient? apiClient;

    public ProfilePage()
    {
        InitializeComponent();
        apiClient = IPlatformApplication.Current?.Services.GetService<ProviderMobileApiClient>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadProfileAsync();
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadProfileAsync();
        RefreshHost.IsRefreshing = false;
    }

    private async Task LoadProfileAsync()
    {
        ContentHost.Clear();
        ContentHost.Add(Text("Chargement du profil...", 15, Colors.Black, false));

        var token = Preferences.Default.Get(AccessTokenPreferenceKey, string.Empty);
        if (string.IsNullOrWhiteSpace(token) || apiClient is null)
        {
            RenderInfo("Connexion requise", "Connectez-vous pour voir votre profil prestataire.");
            return;
        }

        var result = await apiClient.GetProfileAsync(token);
        if (!result.IsSuccess || result.Response is null)
        {
            RenderInfo("Profil indisponible", result.ErrorMessage ?? "Impossible de charger les informations.");
            return;
        }

        RenderProfile(result.Response);
    }

    private void RenderProfile(ProviderMobileProfileResponse profile)
    {
        ContentHost.Clear();

        var completion = profile.ProfileCompletion;
        var completionPercent = completion?.Percent ?? (profile.IsApprovedForMissions ? 100 : 0);

        ContentHost.Add(Card(
            Text(profile.FullName, 26, Colors.Black, true),
            Text($"{profile.EmploymentType} - {profile.CompanyName}", 14, Color.FromArgb("#4B5563"), false),
            Text(profile.IsApprovedForMissions ? "Profil valide pour recevoir des missions." : "Profil a completer avant les missions.", 13, StatusColor(profile.IsApprovedForMissions), true)));

        ContentHost.Add(Card(
            Header("Etat du compte", $"{completionPercent}%"),
            Text($"Telephone : {profile.PhoneNumber}", 14, Colors.Black, false),
            Text($"Adresse : {profile.Address}", 14, Colors.Black, false),
            Text(profile.IsAvailable ? $"Disponible dans un rayon de {profile.MissionRadiusKm} km" : "Indisponible pour le moment", 14, StatusColor(profile.IsAvailable), true)));

        if (completion?.MissingItems.Count > 0)
        {
            ContentHost.Add(Card(
                Text("A completer", 17, Colors.Black, true),
                Text(string.Join(Environment.NewLine, completion.MissingItems), 14, Color.FromArgb("#4B5563"), false)));
        }

        ContentHost.Add(Text("Documents", 18, Colors.Black, true));
        if (profile.Documents.Count == 0)
        {
            ContentHost.Add(Card(Text("Aucun document transmis", 15, Color.FromArgb("#4B5563"), false)));
        }
        else
        {
            foreach (var document in profile.Documents)
            {
                ContentHost.Add(Card(
                    Header(document.Type, document.ContentType),
                    Text(document.OriginalFileName, 14, Colors.Black, false)));
            }
        }
    }

    private void RenderInfo(string title, string message)
    {
        ContentHost.Clear();
        ContentHost.Add(Card(
            Text(title, 20, Colors.Black, true),
            Text(message, 14, Color.FromArgb("#4B5563"), false)));
    }

    private static Border Card(params View[] children)
    {
        return new Border
        {
            BackgroundColor = Colors.White,
            Stroke = Color.FromArgb("#E5E7EB"),
            StrokeShape = new RoundRectangle { CornerRadius = 22 },
            Padding = 16,
            Content = Stack(children)
        };
    }

    private static Grid Header(string left, string right)
    {
        var grid = new Grid { ColumnDefinitions = Columns(GridLength.Star, GridLength.Auto) };
        grid.Add(Text(left, 17, Colors.Black, true), 0);
        grid.Add(Text(right, 13, Color.FromArgb("#2563EB"), true), 1);
        return grid;
    }

    private static VerticalStackLayout Stack(IEnumerable<View> children)
    {
        var stack = new VerticalStackLayout { Spacing = 8 };
        foreach (var child in children)
        {
            stack.Add(child);
        }

        return stack;
    }

    private static ColumnDefinitionCollection Columns(params GridLength[] widths)
    {
        var columns = new ColumnDefinitionCollection();
        foreach (var width in widths)
        {
            columns.Add(new ColumnDefinition(width));
        }

        return columns;
    }

    private static Label Text(string value, double size, Color color, bool bold)
    {
        return new Label
        {
            Text = value,
            FontSize = size,
            TextColor = color,
            FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
            LineBreakMode = LineBreakMode.WordWrap
        };
    }

    private static Color StatusColor(bool isPositive)
    {
        return isPositive ? Color.FromArgb("#008236") : Color.FromArgb("#F97316");
    }
}
