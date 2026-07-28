using HomeService.Contracts.ProviderPortal;
using HomeService.Provider.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace HomeService.Provider.Mobile.Pages;

public partial class ServicesPage : ContentPage
{
    private const string AccessTokenPreferenceKey = "ProviderAccessToken";
    private readonly ProviderMobileApiClient? apiClient;

    public ServicesPage()
    {
        InitializeComponent();
        apiClient = IPlatformApplication.Current?.Services.GetService<ProviderMobileApiClient>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadServicesAsync();
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadServicesAsync();
        RefreshHost.IsRefreshing = false;
    }

    private async Task LoadServicesAsync()
    {
        ContentHost.Clear();
        ContentHost.Add(Text("Chargement des services...", 15, Colors.Black, false));

        var token = Preferences.Default.Get(AccessTokenPreferenceKey, string.Empty);
        if (string.IsNullOrWhiteSpace(token) || apiClient is null)
        {
            RenderInfo("Connexion requise", "Connectez-vous pour voir les services autorises.");
            return;
        }

        var result = await apiClient.GetProfileAsync(token);
        if (!result.IsSuccess || result.Response is null)
        {
            RenderInfo("Services indisponibles", result.ErrorMessage ?? "Impossible de charger les services.");
            return;
        }

        RenderServices(result.Response.Services);
    }

    private void RenderServices(IReadOnlyList<ProviderMobileProfileServiceResponse> services)
    {
        ContentHost.Clear();
        ContentHost.Add(Text("Services autorises", 26, Colors.Black, true));
        ContentHost.Add(Text("Les missions visibles dependent de cette liste et de votre profil.", 14, Color.FromArgb("#4B5563"), false));

        if (services.Count == 0)
        {
            ContentHost.Add(Card(Text("Aucun service actif", 17, Colors.Black, true), Text("Votre entreprise doit encore completer votre fiche.", 14, Color.FromArgb("#4B5563"), false)));
            return;
        }

        foreach (var service in services)
        {
            var statusText = service.CanReceiveMissions
                ? "Assignable"
                : service.RequiresPortfolio
                    ? $"{service.PortfolioPhotoCount}/{service.MinimumPortfolioItems} photos portfolio"
                    : "A completer";

            var stack = new VerticalStackLayout { Spacing = 10 };
            stack.Add(Header(service.ServiceName, statusText, service.CanReceiveMissions));
            stack.Add(Text($"{service.ExperienceLevel} - {service.YearsOfExperience} an(s)", 14, Colors.Black, false));
            stack.Add(Text(service.PriceTier, 13, Color.FromArgb("#4B5563"), false));

            if (service.Prestations.Count > 0)
            {
                stack.Add(Text("Prestations", 13, Color.FromArgb("#6B7280"), true));
                foreach (var prestation in service.Prestations)
                {
                    stack.Add(PrestationRow(prestation));
                }
            }
            else
            {
                stack.Add(Text("Service sans prestation specifique.", 13, Color.FromArgb("#6B7280"), false));
            }

            ContentHost.Add(Card(stack));
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
        return Card(Stack(children));
    }

    private static Border Card(View content)
    {
        return new Border
        {
            BackgroundColor = Colors.White,
            Stroke = Color.FromArgb("#E5E7EB"),
            StrokeShape = new RoundRectangle { CornerRadius = 22 },
            Padding = 16,
            Content = content
        };
    }

    private static Grid Header(string left, string right, bool isReady)
    {
        var grid = new Grid { ColumnDefinitions = Columns(GridLength.Star, GridLength.Auto), ColumnSpacing = 10 };
        grid.Add(Text(left, 18, Colors.Black, true), 0);
        grid.Add(Pill(right, isReady ? Color.FromArgb("#DCFCE7") : Color.FromArgb("#FFEDD5"), isReady ? Color.FromArgb("#008236") : Color.FromArgb("#C2410C")), 1);
        return grid;
    }

    private static Grid PrestationRow(ProviderMobileProfilePrestationResponse prestation)
    {
        var grid = new Grid
        {
            ColumnDefinitions = Columns(GridLength.Star, GridLength.Auto),
            ColumnSpacing = 10,
            Padding = new Thickness(0, 4)
        };

        grid.Add(Text(prestation.Name, 14, Colors.Black, false), 0);
        grid.Add(Text($"{prestation.PriceMinAmount:N0} - {prestation.PriceMaxAmount:N0} {prestation.Currency}", 13, Color.FromArgb("#2563EB"), true), 1);
        return grid;
    }

    private static Border Pill(string text, Color background, Color foreground)
    {
        return new Border
        {
            BackgroundColor = background,
            Stroke = background,
            StrokeShape = new RoundRectangle { CornerRadius = 999 },
            Padding = new Thickness(10, 5),
            Content = Text(text, 12, foreground, true)
        };
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
}
