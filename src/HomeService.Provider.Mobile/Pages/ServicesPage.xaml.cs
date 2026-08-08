using HomeService.Contracts.ProviderPortal;
using HomeService.Provider.Mobile.Services;
using HomeService.Mobile.Shared;
using Microsoft.Maui.Controls.Shapes;

namespace HomeService.Provider.Mobile.Pages;

public partial class ServicesPage : ContentPage
{
    private readonly ProviderMobileApiClient? apiClient;
    private readonly ProviderSessionService? sessionService;
    private readonly CatalogMediaResolver? catalogMedia;

    public ServicesPage()
    {
        InitializeComponent();
        apiClient = IPlatformApplication.Current?.Services.GetService<ProviderMobileApiClient>();
        sessionService = IPlatformApplication.Current?.Services.GetService<ProviderSessionService>();
        catalogMedia = IPlatformApplication.Current?.Services.GetService<CatalogMediaResolver>();
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
        MessageBanner.IsVisible = false;
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;

        var token = sessionService is null ? null : await sessionService.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token) || apiClient is null)
        {
            ShowError("Votre session a expiré. Reconnectez-vous pour charger vos compétences.");
            await RenderServicesAsync([]);
            StopLoading();
            return;
        }

        var result = await apiClient.GetProfileAsync(token);
        StopLoading();
        if (!result.IsSuccess || result.Response is null)
        {
            ShowError(result.ErrorMessage ?? "Impossible de charger les services depuis l’API.");
            await RenderServicesAsync([]);
            return;
        }

        await RenderServicesAsync(result.Response.Services);
    }

    private async Task RenderServicesAsync(IReadOnlyList<ProviderMobileProfileServiceResponse> services)
    {
        ContentHost.Children.Clear();
        if (services.Count == 0)
        {
            ContentHost.Add(new Border
            {
                Style = (Style)Application.Current!.Resources["PremiumCard"],
                Content = new VerticalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        new Image { Source = "service_default.svg", HeightRequest = 44, WidthRequest = 44, HorizontalOptions = LayoutOptions.Start },
                        new Label { Text = "Aucun service actif", FontFamily = "PlusJakartaSans", FontSize = 17, FontAttributes = FontAttributes.Bold },
                        new Label { Text = "Votre entreprise doit vous associer au minimum à un service et à ses prestations.", FontFamily = "PlusJakartaSans", FontSize = 13, TextColor = Color.FromArgb("#667085") }
                    }
                }
            });
            return;
        }

        foreach (var service in services.OrderBy(item => item.ServiceName))
        {
            var content = new VerticalStackLayout { Spacing = 12 };
            var header = new Grid { ColumnDefinitions = Columns(GridLength.Auto, GridLength.Star, GridLength.Auto), ColumnSpacing = 11 };
            var serviceImage = new Image { Source = ProviderIconResolver.ForService(service.IconName, service.ServiceName), WidthRequest = 34, HeightRequest = 34, Aspect = Aspect.AspectFit };
            var remoteServiceImage = catalogMedia is null ? null : await catalogMedia.ResolveServiceAsync(service.ServiceId, service.ServiceName);
            if (remoteServiceImage is not null) serviceImage.Source = remoteServiceImage;
            header.Add(new Border
            {
                WidthRequest = 50,
                HeightRequest = 50,
                BackgroundColor = Color.FromArgb("#EEF4FF"),
                Stroke = Color.FromArgb("#DCE8FF"),
                StrokeShape = new RoundRectangle { CornerRadius = 15 },
                Content = serviceImage
            }, 0);
            header.Add(new VerticalStackLayout
            {
                Spacing = 3,
                Children =
                {
                    new Label { Text = service.ServiceName, FontFamily = "PlusJakartaSans", FontSize = 17, FontAttributes = FontAttributes.Bold },
                    new Label { Text = $"{ExperienceLabel(service.ExperienceLevel)} · {service.YearsOfExperience} an(s)", FontFamily = "PlusJakartaSans", FontSize = 12, TextColor = Color.FromArgb("#667085") }
                }
            }, 1);
            header.Add(CreateStatusPill(service.CanReceiveMissions), 2);
            content.Add(header);

            if (service.RequiresPortfolio)
            {
                content.Add(new Label
                {
                    Text = $"Portfolio : {service.PortfolioPhotoCount}/{service.MinimumPortfolioItems} photo(s)",
                    FontFamily = "PlusJakartaSans",
                    FontSize = 12,
                    TextColor = service.PortfolioPhotoCount >= service.MinimumPortfolioItems ? Color.FromArgb("#16B364") : Color.FromArgb("#B54708")
                });
            }

            content.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#EEF1F6") });
            content.Add(new Label
            {
                Text = service.Prestations.Count == 0 ? "Aucune prestation associée" : $"{service.Prestations.Count} prestation(s)",
                FontFamily = "PlusJakartaSans",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#667085")
            });

            foreach (var prestation in service.Prestations.OrderBy(item => item.Name))
            {
                var row = new Grid { ColumnDefinitions = Columns(GridLength.Auto, GridLength.Star), ColumnSpacing = 9, Padding = new Thickness(0, 3) };
                var prestationImage = new Image { Source = "icon_check.svg", WidthRequest = 30, HeightRequest = 30, Aspect = Aspect.AspectFit, VerticalOptions = LayoutOptions.Start };
                var remotePrestationImage = catalogMedia is null
                    ? null
                    : await catalogMedia.ResolvePrestationAsync(prestation.ServicePrestationId, prestation.Name, service.ServiceId, service.ServiceName);
                if (remotePrestationImage is not null) prestationImage.Source = remotePrestationImage;
                row.Add(prestationImage, 0);
                row.Add(new Label { Text = prestation.Name, FontFamily = "PlusJakartaSans", FontSize = 13, LineBreakMode = LineBreakMode.WordWrap }, 1);
                content.Add(row);
            }

            ContentHost.Add(new Border { Style = (Style)Application.Current!.Resources["PremiumCard"], Content = content });
        }
    }

    private static View CreateStatusPill(bool canReceiveMissions) => new Border
    {
        BackgroundColor = Color.FromArgb(canReceiveMissions ? "#ECFDF3" : "#FFF7ED"),
        Stroke = Color.FromArgb(canReceiveMissions ? "#ABEFC6" : "#FED7AA"),
        StrokeShape = new RoundRectangle { CornerRadius = 11 },
        Padding = new Thickness(8, 5),
        VerticalOptions = LayoutOptions.Center,
        Content = new Label
        {
            Text = canReceiveMissions ? "Validé" : "À compléter",
            FontFamily = "PlusJakartaSans",
            FontSize = 10,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb(canReceiveMissions ? "#067647" : "#B54708")
        }
    };

    private void StopLoading()
    {
        LoadingIndicator.IsRunning = false;
        LoadingIndicator.IsVisible = false;
    }

    private void ShowError(string message)
    {
        MessageLabel.Text = message;
        MessageBanner.IsVisible = true;
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    private static string ExperienceLabel(string value) => value switch
    {
        "Beginner" => "Débutant",
        "Intermediate" => "Intermédiaire",
        "Confirmed" => "Confirmé",
        "Expert" => "Expert",
        _ => value
    };

    private static ColumnDefinitionCollection Columns(params GridLength[] widths)
    {
        var result = new ColumnDefinitionCollection();
        foreach (var width in widths) result.Add(new ColumnDefinition(width));
        return result;
    }
}
