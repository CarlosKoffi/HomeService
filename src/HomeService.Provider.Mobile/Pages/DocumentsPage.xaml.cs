using HomeService.Contracts.ProviderPortal;
using HomeService.Provider.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Storage;

namespace HomeService.Provider.Mobile.Pages;

public partial class DocumentsPage : ContentPage
{
    private readonly ProviderMobileApiClient? apiClient;
    private readonly ProviderSessionService? sessionService;
    private string? accessToken;

    public DocumentsPage()
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
        Render(result.Response?.Documents ?? []);
    }

    private void Render(IReadOnlyList<ProviderMobileProfileDocumentResponse> documents)
    {
        DocumentsStack.Children.Clear();
        if (documents.Count == 0)
        {
            DocumentsStack.Add(new Label { Text = "Aucune pièce transmise.", Style = (Style)Application.Current!.Resources["MutedLabel"] });
            return;
        }

        foreach (var document in documents)
        {
            var grid = new Grid { ColumnDefinitions = Columns(GridLength.Auto, GridLength.Star, GridLength.Auto), ColumnSpacing = 11 };
            grid.Add(new Border { WidthRequest = 44, HeightRequest = 44, BackgroundColor = Color.FromArgb("#EEF4FF"), Stroke = Color.FromArgb("#EEF4FF"), StrokeShape = new RoundRectangle { CornerRadius = 13 }, Content = new Label { Text = DocumentIcon(document.Type), FontSize = 20, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center } }, 0);
            grid.Add(new VerticalStackLayout { Spacing = 3, Children = { new Label { Text = DocumentLabel(document.Type), FontFamily = "PlusJakartaSans", FontAttributes = FontAttributes.Bold, FontSize = 14 }, new Label { Text = document.OriginalFileName, FontFamily = "PlusJakartaSans", FontSize = 12, TextColor = Color.FromArgb("#667085"), LineBreakMode = LineBreakMode.TailTruncation } } }, 1);
            grid.Add(new Label { Text = "Transmis", FontFamily = "PlusJakartaSans", FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#16B364"), VerticalTextAlignment = TextAlignment.Center }, 2);
            DocumentsStack.Add(new Border { Style = (Style)Application.Current!.Resources["PremiumCard"], Content = grid });
        }
    }

    private async Task UploadAsync(string documentType)
    {
        if (apiClient is null || string.IsNullOrWhiteSpace(accessToken)) return;
        try
        {
            var file = documentType == "Photo"
                ? await MediaPicker.Default.PickPhotoAsync()
                : await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Choisir un fichier" });
            if (file is null) return;

            MessageLabel.Text = "Envoi en cours…";
            MessageBanner.IsVisible = true;
            var result = await apiClient.UploadDocumentAsync(accessToken, documentType, file);
            MessageLabel.Text = result.IsSuccess ? "Document ajouté avec succès." : result.ErrorMessage ?? "Envoi impossible.";
            if (result.IsSuccess) await LoadAsync();
        }
        catch (FeatureNotSupportedException)
        {
            MessageLabel.Text = "La galerie n'est pas disponible sur cet appareil.";
            MessageBanner.IsVisible = true;
        }
        catch (PermissionException)
        {
            MessageLabel.Text = "Autorisez l'accès aux fichiers et aux photos dans les réglages du téléphone.";
            MessageBanner.IsVisible = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageLabel.Text = "Ce fichier ne peut pas être lu. Choisissez-le de nouveau.";
            MessageBanner.IsVisible = true;
        }
    }

    private async void OnPhotoClicked(object? sender, EventArgs e) => await UploadAsync("Photo");
    private async void OnIdentityClicked(object? sender, EventArgs e) => await UploadAsync("IdentityDocument");
    private async void OnDiplomaClicked(object? sender, EventArgs e) => await UploadAsync("Diploma");
    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
    private static string DocumentLabel(string type) => type switch { "Photo" => "Photo de profil", "IdentityDocument" => "Pièce d’identité", "Diploma" => "Diplôme / attestation", _ => type };
    private static string DocumentIcon(string type) => type switch { "Photo" => "◉", "IdentityDocument" => "▤", "Diploma" => "♢", _ => "□" };
    private static ColumnDefinitionCollection Columns(params GridLength[] widths) { var result = new ColumnDefinitionCollection(); foreach (var width in widths) result.Add(new ColumnDefinition(width)); return result; }
}
