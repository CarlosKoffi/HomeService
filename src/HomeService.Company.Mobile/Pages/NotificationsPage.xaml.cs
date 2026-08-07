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

    private async void OnNotificationSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not NotificationRow row) return;
        NotificationsView.SelectedItem = null;
        var token = await sessionStore.GetTokenAsync();
        var companyId = await sessionStore.GetCompanyIdAsync();
        if (string.IsNullOrWhiteSpace(token) || !companyId.HasValue) return;
        if (!row.Source.IsRead)
        {
            await apiClient.MarkNotificationReadAsync(token, companyId.Value, row.Source.Id);
        }

        if (!string.IsNullOrWhiteSpace(row.Source.ActionUrl)
            && row.Source.ActionUrl.StartsWith("missions/", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(row.Source.ActionUrl["missions/".Length..], out var missionId))
        {
            await Shell.Current.GoToAsync($"{nameof(MissionDetailPage)}?missionId={missionId:D}");
            return;
        }

        if (row.Source.Type.Contains("Provider", StringComparison.OrdinalIgnoreCase)
            || row.Source.ActionUrl?.Contains("provider", StringComparison.OrdinalIgnoreCase) == true)
        {
            if (row.Source.ActionUrl?.StartsWith("providers/", StringComparison.OrdinalIgnoreCase) == true
                && Guid.TryParse(row.Source.ActionUrl["providers/".Length..], out var requestId))
            {
                await Shell.Current.GoToAsync($"{nameof(ProviderCandidateDetailPage)}?requestId={requestId:D}");
                return;
            }

            await Shell.Current.GoToAsync("//providers");
            return;
        }

        await LoadAsync();
    }

    public sealed record NotificationRow(
        CompanyPortalNotificationResponse Source,
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
                item,
                item.Title,
                item.Message,
                item.OccurredAt.ToLocalTime().ToString("dd/MM/yyyy · HH:mm"),
                item.Type.Contains("Provider", StringComparison.OrdinalIgnoreCase) ? "icon_user.svg" : "icon_mission.svg",
                urgent ? Color.FromArgb("#EEF4FF") : Colors.White,
                urgent ? Color.FromArgb("#B8CCFF") : Color.FromArgb("#DCE8FF"));
        }
    }
}
