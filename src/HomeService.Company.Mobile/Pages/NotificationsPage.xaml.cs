using System.Collections.ObjectModel;
using HomeService.Company.Mobile.Services;
using HomeService.Contracts.CompanyPortal;

namespace HomeService.Company.Mobile.Pages;

public partial class NotificationsPage : ContentPage
{
    private readonly CompanyMobileApiClient apiClient;
    private readonly CompanySessionStore sessionStore;
    private readonly ObservableCollection<NotificationRow> notifications = [];

    public NotificationsPage()
    {
        InitializeComponent();
        apiClient = IPlatformApplication.Current!.Services.GetRequiredService<CompanyMobileApiClient>();
        sessionStore = IPlatformApplication.Current.Services.GetRequiredService<CompanySessionStore>();
        NotificationsView.ItemsSource = notifications;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var token = await sessionStore.GetTokenAsync();
        var companyId = await sessionStore.GetCompanyIdAsync();
        if (string.IsNullOrWhiteSpace(token) || !companyId.HasValue) return;
        var result = await apiClient.GetNotificationsAsync(token, companyId.Value);
        if (!result.IsSuccess || result.Response is null) return;
        UnreadLabel.Text = result.Response.UnreadCount == 0 ? "Tout est à jour" : $"{result.Response.UnreadCount} action(s) non lue(s)";
        notifications.Clear();
        foreach (var notification in result.Response.Notifications.OrderByDescending(item => item.OccurredAt))
        {
            notifications.Add(NotificationRow.From(notification));
        }
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadAsync();
        RefreshHost.IsRefreshing = false;
    }

    public sealed record NotificationRow(
        string Title,
        string Message,
        string TimeLabel,
        string Icon,
        Color IconBackground,
        Color StrokeColor)
    {
        public static NotificationRow From(CompanyPortalNotificationResponse item)
        {
            var urgent = item.Tone is "warning" or "danger" || !item.IsRead;
            return new NotificationRow(
                item.Title,
                item.Message,
                item.OccurredAt.ToLocalTime().ToString("dd/MM/yyyy · HH:mm"),
                item.Type.Contains("Provider", StringComparison.OrdinalIgnoreCase) ? "icon_user.svg" : "icon_mission.svg",
                urgent ? Color.FromArgb("#EEF4FF") : Colors.White,
                urgent ? Color.FromArgb("#B8CCFF") : Color.FromArgb("#DCE8FF"));
        }
    }
}
