using HomeService.Contracts.ProviderPortal;
using HomeService.Provider.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace HomeService.Provider.Mobile.Pages;

public partial class NotificationsPage : ContentPage
{
    private readonly ProviderMobileApiClient? apiClient;
    private readonly ProviderSessionService? sessionService;
    private string? accessToken;
    private bool unreadOnly;

    public NotificationsPage()
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
        var result = await apiClient.GetNotificationsAsync(accessToken, unreadOnly);
        if (result.Response is null)
        {
            Render([]);
            return;
        }

        UnreadSummaryLabel.Text = result.Response.UnreadCount == 0 ? "Vous êtes à jour." : $"{result.Response.UnreadCount} notification(s) non lue(s)";
        Render(result.Response.Items);
    }

    private void Render(IReadOnlyList<ProviderMobileNotificationResponse> notifications)
    {
        NotificationsStack.Children.Clear();
        if (notifications.Count == 0)
        {
            NotificationsStack.Add(new Label { Text = "Aucune notification.", Style = (Style)Application.Current!.Resources["MutedLabel"] });
            return;
        }

        foreach (var notification in notifications)
        {
            var card = new Border
            {
                BackgroundColor = Colors.White,
                Stroke = notification.IsRead ? Color.FromArgb("#DCE8FF") : Color.FromArgb("#155EEF"),
                StrokeShape = new RoundRectangle { CornerRadius = 17 },
                Padding = 13,
                Content = new Grid
                {
                    ColumnDefinitions = Columns(GridLength.Auto, GridLength.Star, GridLength.Auto),
                    ColumnSpacing = 11
                }
            };
            var grid = (Grid)card.Content;
            grid.Add(new Border
            {
                BackgroundColor = Color.FromArgb("#EEF4FF"), Stroke = Color.FromArgb("#EEF4FF"), StrokeShape = new RoundRectangle { CornerRadius = 13 }, WidthRequest = 44, HeightRequest = 44,
                Content = new Label { Text = IconFor(notification), FontSize = 20, TextColor = Color.FromArgb("#155EEF"), HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center }
            }, 0);
            grid.Add(new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new Label { Text = notification.Title, FontFamily = "PlusJakartaSans", FontSize = 15, FontAttributes = FontAttributes.Bold },
                    new Label { Text = notification.Body, FontFamily = "PlusJakartaSans", FontSize = 13, TextColor = Color.FromArgb("#667085"), LineHeight = 1.2 },
                    new Label { Text = notification.CreatedAt.LocalDateTime.ToString("dd.MM.yyyy · HH:mm"), FontFamily = "PlusJakartaSans", FontSize = 11, TextColor = Color.FromArgb("#667085") }
                }
            }, 1);
            grid.Add(new Label { Text = notification.IsRead ? string.Empty : "Nouveau", FontFamily = "PlusJakartaSans", FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#155EEF"), VerticalTextAlignment = TextAlignment.Start }, 2);
            if (!notification.IsRead)
            {
                var tap = new TapGestureRecognizer { CommandParameter = notification.Id };
                tap.Tapped += OnNotificationTapped;
                card.GestureRecognizers.Add(tap);
            }
            NotificationsStack.Add(card);
        }
    }

    private async void OnNotificationTapped(object? sender, TappedEventArgs e)
    {
        if (apiClient is null || string.IsNullOrWhiteSpace(accessToken) || e.Parameter is not Guid id) return;
        await apiClient.MarkNotificationReadAsync(accessToken, id);
        await LoadAsync();
    }

    private async void OnAllClicked(object? sender, EventArgs e) { unreadOnly = false; AllButton.Style = (Style)Application.Current!.Resources["PrimaryButton"]; UnreadButton.Style = (Style)Application.Current.Resources["SecondaryButton"]; await LoadAsync(); }
    private async void OnUnreadClicked(object? sender, EventArgs e) { unreadOnly = true; UnreadButton.Style = (Style)Application.Current!.Resources["PrimaryButton"]; AllButton.Style = (Style)Application.Current.Resources["SecondaryButton"]; await LoadAsync(); }
    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
    private static string IconFor(ProviderMobileNotificationResponse notification) => notification.RelatedEntityType switch { "Mission" => "▣", "ProviderProfile" => "♙", _ => "◇" };
    private static ColumnDefinitionCollection Columns(params GridLength[] widths) { var result = new ColumnDefinitionCollection(); foreach (var width in widths) result.Add(new ColumnDefinition(width)); return result; }
}
