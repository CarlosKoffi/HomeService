using HomeService.Contracts.ProviderPortal;
using HomeService.Provider.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace HomeService.Provider.Mobile.Pages;

public partial class ServicesPage : ContentPage
{
    private readonly ProviderMobileApiClient? apiClient;
    private readonly ProviderSessionService? sessionService;

    public ServicesPage()
    {
        InitializeComponent();
        apiClient = IPlatformApplication.Current?.Services.GetService<ProviderMobileApiClient>();
        sessionService = IPlatformApplication.Current?.Services.GetService<ProviderSessionService>();
    }

    protected override async void OnAppearing() { base.OnAppearing(); await LoadServicesAsync(); }
    private async void OnRefreshing(object? sender, EventArgs e) { await LoadServicesAsync(); RefreshHost.IsRefreshing = false; }

    private async Task LoadServicesAsync()
    {
        var token = sessionService is null ? null : await sessionService.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token) || apiClient is null) return;
        var result = await apiClient.GetProfileAsync(token);
        RenderServices(result.Response?.Services ?? []);
    }

    private void RenderServices(IReadOnlyList<ProviderMobileProfileServiceResponse> services)
    {
        ContentHost.Children.Clear();
        if (services.Count == 0)
        {
            ContentHost.Add(new Label { Text = "Aucun service actif. Votre entreprise doit compléter votre fiche.", Style = (Style)Application.Current!.Resources["MutedLabel"] });
            return;
        }

        foreach (var service in services)
        {
            var stack = new VerticalStackLayout { Spacing = 9 };
            var header = new Grid { ColumnDefinitions = Columns(GridLength.Auto, GridLength.Star, GridLength.Auto), ColumnSpacing = 10 };
            header.Add(new Border { WidthRequest = 46, HeightRequest = 46, BackgroundColor = Color.FromArgb("#EEF4FF"), Stroke = Color.FromArgb("#DCE8FF"), StrokeShape = new RoundRectangle { CornerRadius = 14 }, Content = new Label { Text = ServiceIcon(service.IconName), FontSize = 21, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center } }, 0);
            header.Add(new VerticalStackLayout { Spacing = 3, Children = { new Label { Text = service.ServiceName, FontFamily = "PlusJakartaSans", FontSize = 17, FontAttributes = FontAttributes.Bold }, new Label { Text = $"{ExperienceLabel(service.ExperienceLevel)} · {service.YearsOfExperience} an(s)", FontFamily = "PlusJakartaSans", FontSize = 12, TextColor = Color.FromArgb("#667085") } } }, 1);
            header.Add(new Label { Text = service.CanReceiveMissions ? "Validé" : "À compléter", FontFamily = "PlusJakartaSans", FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = service.CanReceiveMissions ? Color.FromArgb("#16B364") : Color.FromArgb("#B54708"), VerticalTextAlignment = TextAlignment.Center }, 2);
            stack.Add(header);

            if (service.RequiresPortfolio)
            {
                stack.Add(new Label { Text = $"Portfolio : {service.PortfolioPhotoCount}/{service.MinimumPortfolioItems} photo(s)", FontFamily = "PlusJakartaSans", FontSize = 12, TextColor = Color.FromArgb("#667085") });
            }

            foreach (var prestation in service.Prestations)
            {
                var prestationRow = new Grid
                {
                    ColumnDefinitions = Columns(GridLength.Auto, GridLength.Star),
                    ColumnSpacing = 9,
                    Padding = new Thickness(2, 5)
                };
                prestationRow.Add(new Label { Text = "✓", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#16B364") }, 0);
                prestationRow.Add(new Label { Text = prestation.Name, FontFamily = "PlusJakartaSans", FontSize = 13 }, 1);
                stack.Add(prestationRow);
            }

            ContentHost.Add(new Border { Style = (Style)Application.Current!.Resources["PremiumCard"], Content = stack });
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
    private static string ExperienceLabel(string value) => value switch { "Beginner" => "Débutant", "Intermediate" => "Intermédiaire", "Confirmed" => "Confirmé", "Expert" => "Expert", _ => value };
    private static string ServiceIcon(string iconName) => iconName.ToLowerInvariant() switch { var value when value.Contains("plumb") => "♨", var value when value.Contains("electric") => "ϟ", var value when value.Contains("clean") => "✦", _ => "◇" };
    private static ColumnDefinitionCollection Columns(params GridLength[] widths) { var result = new ColumnDefinitionCollection(); foreach (var width in widths) result.Add(new ColumnDefinition(width)); return result; }
}
