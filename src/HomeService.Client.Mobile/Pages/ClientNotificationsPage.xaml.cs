using System.Collections.ObjectModel;
using System.Text.Json;
using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

public partial class ClientNotificationsPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
    private readonly ClientNotificationState notificationState = MobileServiceLocator.GetRequiredService<ClientNotificationState>();
    private readonly ObservableCollection<NotificationGroup> groups = [];
    private bool unreadOnly;

    public ClientNotificationsPage()
    {
        InitializeComponent();
        NotificationsView.ItemsSource = groups;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        groups.Clear();
        var result = await apiClient.GetNotificationsAsync(unreadOnly);
        if (result.IsSuccess && result.Response is not null)
        {
            notificationState.SetUnreadCount(result.Response.UnreadCount);
            UnreadLabel.Text = result.Response.UnreadCount == 0
                ? string.Empty
                : $"{result.Response.UnreadCount} nouvelle(s)";

            var rows = result.Response.Notifications.Select(NotificationRow.From).ToList();
            var today = rows.Where(row => row.LocalDate.Date == DateTime.Today).ToList();
            var earlier = rows.Where(row => row.LocalDate.Date != DateTime.Today).ToList();
            if (today.Count > 0)
            {
                groups.Add(new NotificationGroup("Aujourd'hui", today));
            }

            if (earlier.Count > 0)
            {
                groups.Add(new NotificationGroup("Plus tôt", earlier));
            }
        }

        EmptyState.IsVisible = groups.Count == 0;
    }

    private async void OnSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not NotificationRow row)
        {
            return;
        }

        NotificationsView.SelectedItem = null;
        if (!row.Source.IsRead)
        {
            await apiClient.MarkNotificationReadAsync(row.Source.Id);
        }

        var route = await TryResolveActionRouteAsync(row.Source);
        if (!string.IsNullOrWhiteSpace(route))
        {
            await Shell.Current.GoToAsync(route);
            return;
        }

        await DisplayAlert(row.Title, row.Body, "Fermer");
        await LoadAsync();
    }

    private static async Task<string?> TryResolveActionRouteAsync(ClientNotificationResponse notification)
    {
        Guid? missionId = notification.RelatedEntityType == "Mission"
            ? notification.RelatedEntityId
            : null;
        string? type = null;

        if (!string.IsNullOrWhiteSpace(notification.MetadataJson))
        {
            try
            {
                using var metadata = JsonDocument.Parse(notification.MetadataJson);
                if (metadata.RootElement.TryGetProperty("type", out var typeElement))
                {
                    type = typeElement.GetString();
                }

                if (metadata.RootElement.TryGetProperty("missionId", out var missionElement)
                    && Guid.TryParse(missionElement.GetString(), out var parsedMissionId))
                {
                    missionId = parsedMissionId;
                }
            }
            catch (JsonException)
            {
                return null;
            }
        }

        if (missionId is null)
        {
            return null;
        }

        return await ClientNotificationNavigationService.ResolveMissionRouteAsync(missionId.Value, type);
    }

    private async void OnAllClicked(object sender, EventArgs e)
    {
        unreadOnly = false;
        SetFilter(AllButton);
        await LoadAsync();
    }

    private async void OnUnreadClicked(object sender, EventArgs e)
    {
        unreadOnly = true;
        SetFilter(UnreadButton);
        await LoadAsync();
    }

    private void SetFilter(Button selected)
    {
        var blue = (Color)Application.Current!.Resources["WeleBlue"];
        var secondary = (Color)Application.Current.Resources["ListSecondary"];
        foreach (var button in new[] { AllButton, UnreadButton })
        {
            var active = button == selected;
            button.BackgroundColor = active ? Colors.White : Colors.Transparent;
            button.TextColor = active ? blue : secondary;
            button.BorderWidth = 0;
        }
    }

    private async void OnBackClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    private sealed class NotificationGroup(string title, IEnumerable<NotificationRow> rows)
        : ObservableCollection<NotificationRow>(rows)
    {
        public string Title { get; } = title;
    }

    private sealed record NotificationRow(
        ClientNotificationResponse Source,
        string Title,
        string Body,
        string DateText,
        DateTime LocalDate,
        bool IsUnread,
        string IconSource,
        Color IconBackground)
    {
        public static NotificationRow From(ClientNotificationResponse item)
        {
            var localDate = item.ScheduledAt.LocalDateTime;
            var dateText = localDate.Date == DateTime.Today
                ? $"Aujourd'hui · {localDate:HH:mm}"
                : localDate.Date == DateTime.Today.AddDays(-1)
                    ? $"Hier · {localDate:HH:mm}"
                    : localDate.ToString("dd MMM · HH:mm");
            var (icon, background) = ResolveVisual(item);
            return new NotificationRow(item, item.Title, item.Body, dateText, localDate, !item.IsRead, icon, background);
        }

        private static (string Icon, Color Background) ResolveVisual(ClientNotificationResponse item)
        {
            var content = $"{item.Title} {item.MetadataJson}".ToLowerInvariant();
            if (content.Contains("review") || content.Contains("avis") || content.Contains("rating"))
            {
                return ("profile_review.svg", Color.FromArgb("#FFF7DF"));
            }

            if (content.Contains("provider") || content.Contains("technicien") || content.Contains("prestataire"))
            {
                return ("nav_profile_active.svg", Color.FromArgb("#F2EDFF"));
            }

            if (content.Contains("complete") || content.Contains("termin"))
            {
                return ("nav_requests_active.svg", Color.FromArgb("#E9F8F2"));
            }

            return ("nav_requests_active.svg", Color.FromArgb("#EEF4FF"));
        }
    }
}
