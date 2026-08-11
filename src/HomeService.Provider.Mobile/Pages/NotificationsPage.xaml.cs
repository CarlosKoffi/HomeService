using HomeService.Contracts.ProviderPortal;
using HomeService.Provider.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;
using System.Text.Json;

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

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadAsync();
        RefreshHost.IsRefreshing = false;
    }

    private async Task LoadAsync()
    {
        SetLoading(true);
        MessageBanner.IsVisible = false;
        accessToken = sessionService is null ? null : await sessionService.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(accessToken) || apiClient is null)
        {
            SetLoading(false);
            ShowMessage("Votre session a expiré. Reconnectez-vous pour voir les notifications.");
            Render([]);
            return;
        }

        var result = await apiClient.GetNotificationsAsync(accessToken, unreadOnly);
        SetLoading(false);
        if (!result.IsSuccess || result.Response is null)
        {
            ShowMessage(result.ErrorMessage ?? "Impossible de charger les notifications depuis l’API.");
            Render([]);
            return;
        }

        UnreadSummaryLabel.Text = result.Response.UnreadCount == 0
            ? "Vous êtes à jour."
            : result.Response.UnreadCount == 1 ? "1 notification non lue" : $"{result.Response.UnreadCount} notifications non lues";
        MarkAllReadButton.IsVisible = result.Response.UnreadCount > 0;
        Render(result.Response.Items);
    }

    private async void OnMarkAllReadClicked(object? sender, EventArgs e)
    {
        if (apiClient is null || string.IsNullOrWhiteSpace(accessToken)) return;
        MarkAllReadButton.IsEnabled = false;
        try
        {
            var result = await apiClient.MarkAllNotificationsReadAsync(accessToken);
            if (!result.IsSuccess)
            {
                ShowMessage(result.ErrorMessage ?? "Impossible de marquer les notifications comme lues.");
                return;
            }

            await LoadAsync();
        }
        finally
        {
            MarkAllReadButton.IsEnabled = true;
        }
    }

    private void Render(IReadOnlyList<ProviderMobileNotificationResponse> notifications)
    {
        NotificationsStack.Children.Clear();
        if (notifications.Count == 0)
        {
            NotificationsStack.Add(new Border
            {
                Style = (Style)Application.Current!.Resources["PremiumCard"],
                Content = new VerticalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        new Image { Source = "app_bell.svg", WidthRequest = 42, HeightRequest = 42, HorizontalOptions = LayoutOptions.Center },
                        new Label { Text = unreadOnly ? "Aucune notification non lue" : "Aucune notification", FontFamily = "PlusJakartaSans", FontSize = 16, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center },
                        new Label { Text = "Les nouvelles missions et les changements importants apparaîtront ici.", FontFamily = "PlusJakartaSans", FontSize = 13, TextColor = Color.FromArgb("#667085"), HorizontalTextAlignment = TextAlignment.Center }
                    }
                }
            });
            return;
        }

        foreach (var notification in notifications)
        {
            var icon = new Border
            {
                BackgroundColor = Color.FromArgb("#EEF4FF"),
                Stroke = Color.FromArgb("#DCE8FF"),
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                WidthRequest = 46,
                HeightRequest = 46,
                Content = new Image { Source = ProviderIconResolver.ForNotification(notification.RelatedEntityType, notification.Title), WidthRequest = 24, HeightRequest = 24 }
            };
            var text = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new Label { Text = notification.Title, FontFamily = "PlusJakartaSans", FontSize = 15, FontAttributes = FontAttributes.Bold },
                    new Label { Text = notification.Body, FontFamily = "PlusJakartaSans", FontSize = 13, TextColor = Color.FromArgb("#667085"), LineHeight = 1.2 },
                    new Label { Text = notification.CreatedAt.LocalDateTime.ToString("dd.MM.yyyy · HH:mm"), FontFamily = "PlusJakartaSans", FontSize = 11, TextColor = Color.FromArgb("#98A2B3") }
                }
            };
            var unreadMarker = new Border
            {
                IsVisible = !notification.IsRead,
                BackgroundColor = Color.FromArgb("#155EEF"),
                Stroke = Color.FromArgb("#155EEF"),
                StrokeShape = new RoundRectangle { CornerRadius = 5 },
                WidthRequest = 9,
                HeightRequest = 9,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, 5, 0, 0)
            };
            var grid = new Grid { ColumnDefinitions = Columns(GridLength.Auto, GridLength.Star, GridLength.Auto), ColumnSpacing = 11 };
            grid.Add(icon, 0);
            grid.Add(text, 1);
            grid.Add(unreadMarker, 2);

            var card = new Border
            {
                BackgroundColor = Colors.White,
                Stroke = Color.FromArgb(notification.IsRead ? "#E6E9EF" : "#CFE0FF"),
                StrokeShape = new RoundRectangle { CornerRadius = 17 },
                Padding = 13,
                Content = grid
            };
            var tap = new TapGestureRecognizer { CommandParameter = notification };
            tap.Tapped += OnNotificationTapped;
            card.GestureRecognizers.Add(tap);
            NotificationsStack.Add(card);
        }
    }

    private async void OnNotificationTapped(object? sender, TappedEventArgs e)
    {
        if (apiClient is null || string.IsNullOrWhiteSpace(accessToken) || e.Parameter is not ProviderMobileNotificationResponse notification) return;
        if (!notification.IsRead) await apiClient.MarkNotificationReadAsync(accessToken, notification.Id);

        if (!string.IsNullOrWhiteSpace(notification.MetadataJson))
        {
            try
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(notification.MetadataJson);
                if (data is not null)
                {
                    ProviderNotificationNavigationService.Store(data);
                    if (await ProviderNotificationNavigationService.TryNavigateAsync()) return;
                }
            }
            catch (JsonException)
            {
                // Older notifications fall back to their related entity below.
            }
        }

        if (notification.RelatedEntityId is not null
            && string.Equals(notification.RelatedEntityType, "Mission", StringComparison.OrdinalIgnoreCase))
        {
            var missionsResult = await apiClient.GetMissionsAsync(accessToken);
            var assignment = missionsResult.Response?.Items.FirstOrDefault(item =>
                item.MissionId == notification.RelatedEntityId || item.AssignmentId == notification.RelatedEntityId);
            if (assignment is not null)
            {
                await Shell.Current.GoToAsync($"{nameof(MissionDetailPage)}?assignmentId={assignment.AssignmentId:D}");
                return;
            }
        }

        await LoadAsync();
    }

    private async void OnAllClicked(object? sender, EventArgs e)
    {
        unreadOnly = false;
        AllButton.Style = (Style)Application.Current!.Resources["PrimaryButton"];
        UnreadButton.Style = (Style)Application.Current.Resources["SecondaryButton"];
        await LoadAsync();
    }

    private async void OnUnreadClicked(object? sender, EventArgs e)
    {
        unreadOnly = true;
        UnreadButton.Style = (Style)Application.Current!.Resources["PrimaryButton"];
        AllButton.Style = (Style)Application.Current.Resources["SecondaryButton"];
        await LoadAsync();
    }

    private void SetLoading(bool value)
    {
        LoadingIndicator.IsVisible = value;
        LoadingIndicator.IsRunning = value;
    }

    private void ShowMessage(string value)
    {
        MessageLabel.Text = value;
        MessageBanner.IsVisible = true;
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    private static ColumnDefinitionCollection Columns(params GridLength[] widths)
    {
        var result = new ColumnDefinitionCollection();
        foreach (var width in widths) result.Add(new ColumnDefinition(width));
        return result;
    }
}
