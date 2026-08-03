using System.Collections.ObjectModel;
using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

public partial class ClientNotificationsPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
    private readonly ClientNotificationState notificationState = MobileServiceLocator.GetRequiredService<ClientNotificationState>();
    private readonly ObservableCollection<NotificationRow> rows = [];
    private bool unreadOnly;
    public ClientNotificationsPage() { InitializeComponent(); NotificationsView.ItemsSource = rows; }
    protected override async void OnAppearing() { base.OnAppearing(); await LoadAsync(); }
    private async Task LoadAsync()
    {
        rows.Clear(); var result = await apiClient.GetNotificationsAsync(unreadOnly);
        if (result.IsSuccess && result.Response is not null)
        { notificationState.SetUnreadCount(result.Response.UnreadCount); UnreadLabel.Text = result.Response.UnreadCount == 0 ? string.Empty : $"{result.Response.UnreadCount} non lue(s)"; foreach (var item in result.Response.Notifications) rows.Add(NotificationRow.From(item)); }
        EmptyState.IsVisible = rows.Count == 0;
    }
    private async void OnSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not NotificationRow row) return;
        NotificationsView.SelectedItem = null;
        if (!row.Source.IsRead) await apiClient.MarkNotificationReadAsync(row.Source.Id);
        await DisplayAlert(row.Title, row.Body, "Fermer"); await LoadAsync();
    }
    private async void OnAllClicked(object sender, EventArgs e) { unreadOnly = false; SetFilter(AllButton); await LoadAsync(); }
    private async void OnUnreadClicked(object sender, EventArgs e) { unreadOnly = true; SetFilter(UnreadButton); await LoadAsync(); }
    private void SetFilter(Button selected) { foreach (var button in new[] { AllButton, UnreadButton }) { var active = button == selected; button.BackgroundColor = active ? (Color)Application.Current!.Resources["WeleBlue"] : Colors.White; button.TextColor = active ? Colors.White : (Color)Application.Current!.Resources["Ink"]; } }
    private async void OnBackClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");
    private sealed record NotificationRow(ClientNotificationResponse Source, string Title, string Body, string DateText, string ReadStatus, Color Background)
    { public static NotificationRow From(ClientNotificationResponse item) => new(item, item.Title, item.Body, item.ScheduledAt.LocalDateTime.ToString("dd/MM/yyyy HH:mm"), item.IsRead ? string.Empty : "Nouveau", item.IsRead ? Colors.White : Color.FromArgb("#F2F6FF")); }
}
