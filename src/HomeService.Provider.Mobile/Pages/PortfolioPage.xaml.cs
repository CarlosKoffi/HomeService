using HomeService.Contracts.ProviderPortal;
using HomeService.Provider.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Storage;

namespace HomeService.Provider.Mobile.Pages;

public partial class PortfolioPage : ContentPage
{
    private readonly ProviderMobileApiClient? apiClient;
    private readonly ProviderSessionService? sessionService;
    private string? accessToken;
    private IReadOnlyList<ProviderMobileProfileServiceResponse> services = [];

    public PortfolioPage()
    {
        InitializeComponent();
        apiClient = IPlatformApplication.Current?.Services.GetService<ProviderMobileApiClient>();
        sessionService = IPlatformApplication.Current?.Services.GetService<ProviderSessionService>();
    }

    protected override async void OnAppearing() { base.OnAppearing(); await LoadAsync(); }
    private async void OnRefreshing(object? sender, EventArgs e) { await LoadAsync(); RefreshHost.IsRefreshing = false; }

    private async Task LoadAsync()
    {
        accessToken = sessionService is null ? null : await sessionService.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(accessToken) || apiClient is null) return;
        var result = await apiClient.GetProfileAsync(accessToken);
        if (result.Response is null) return;
        services = result.Response.Services;
        ServicePicker.ItemsSource = services.Select(item => item.ServiceName).ToList();
        if (ServicePicker.SelectedIndex < 0 && result.Response.Services.Count > 0) ServicePicker.SelectedIndex = 0;
        await RenderAsync(result.Response.PortfolioItems);
    }

    private async Task RenderAsync(IReadOnlyList<ProviderMobilePortfolioItemResponse> items)
    {
        PortfolioGrid.Children.Clear();
        PortfolioGrid.RowDefinitions.Clear();
        if (items.Count == 0)
        {
            PortfolioGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var empty = new Label { Text = "Aucune réalisation ajoutée.", Style = (Style)Application.Current!.Resources["MutedLabel"] };
            PortfolioGrid.Add(empty, 0, 0);
            Grid.SetColumnSpan(empty, 2);
            return;
        }

        for (var index = 0; index < items.Count; index++)
        {
            if (index % 2 == 0) PortfolioGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var item = items[index];
            var image = new Image { Source = "wele_profile_placeholder.svg", HeightRequest = 118, Aspect = Aspect.AspectFill };
            var card = new Border
            {
                BackgroundColor = Colors.White,
                Stroke = Color.FromArgb("#DCE8FF"),
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                Padding = 7,
                Content = new VerticalStackLayout
                {
                    Spacing = 7,
                    Children =
                    {
                        image,
                        new Label { Text = item.ServiceName, FontFamily = "PlusJakartaSans", FontSize = 13, FontAttributes = FontAttributes.Bold },
                        new Label { Text = item.Status == "Approved" ? "Validé" : item.Status == "Rejected" ? "Refusé" : "En attente", FontFamily = "PlusJakartaSans", FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = item.Status == "Approved" ? Color.FromArgb("#16B364") : Color.FromArgb("#B54708") }
                    }
                }
            };
            PortfolioGrid.Add(card, index % 2, index / 2);
            if (apiClient is not null && !string.IsNullOrWhiteSpace(accessToken))
            {
                var photo = await apiClient.DownloadAsync(accessToken, item.PreviewUrl);
                if (photo.Response is { Length: > 0 } bytes) image.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
            }
        }
    }

    private async void OnAddPhotoClicked(object? sender, EventArgs e)
    {
        if (apiClient is null || string.IsNullOrWhiteSpace(accessToken) || ServicePicker.SelectedIndex < 0 || ServicePicker.SelectedIndex >= services.Count)
        {
            MessageLabel.Text = "Choisissez d’abord un service.";
            MessageBanner.IsVisible = true;
            return;
        }

        try
        {
            var service = services[ServicePicker.SelectedIndex];
            var file = await MediaPicker.Default.PickPhotoAsync();
            if (file is null) return;

            MessageLabel.Text = "Envoi en cours…";
            MessageBanner.IsVisible = true;
            var result = await apiClient.UploadPortfolioAsync(accessToken, service.ServiceId, file);
            MessageLabel.Text = result.IsSuccess ? "Réalisation ajoutée." : result.ErrorMessage ?? "Envoi impossible.";
            if (result.IsSuccess) await LoadAsync();
        }
        catch (FeatureNotSupportedException)
        {
            MessageLabel.Text = "La galerie n'est pas disponible sur cet appareil.";
            MessageBanner.IsVisible = true;
        }
        catch (PermissionException)
        {
            MessageLabel.Text = "Autorisez l'accès aux photos dans les réglages du téléphone.";
            MessageBanner.IsVisible = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageLabel.Text = "Cette photo ne peut pas être lue. Choisissez-la de nouveau.";
            MessageBanner.IsVisible = true;
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
