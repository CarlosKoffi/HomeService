using System.Collections.ObjectModel;
using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

public partial class RequestsPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient;
    private readonly ClientSessionStore sessionStore;
    private readonly ObservableCollection<MissionRow> missions = [];
    private string? currentStatus;

    public RequestsPage()
    {
        InitializeComponent();
        apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
        sessionStore = MobileServiceLocator.GetRequiredService<ClientSessionStore>();
        MissionsView.ItemsSource = missions;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        missions.Clear();
        if (!sessionStore.HasSession())
        {
            EmptyState.IsVisible = true;
            return;
        }

        var result = await apiClient.GetMissionsAsync(currentStatus);
        if (result.IsSuccess && result.Response is not null)
        {
            foreach (var item in result.Response)
            {
                missions.Add(MissionRow.From(item));
            }
        }

        EmptyState.IsVisible = missions.Count == 0;
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        await LoadAsync();
        Refresh.IsRefreshing = false;
    }

    private async void OnAllClicked(object sender, EventArgs e)
    {
        currentStatus = null;
        AllButton.BackgroundColor = (Color)Application.Current!.Resources["WeleBlue"];
        AllButton.TextColor = Colors.White;
        ActiveButton.BackgroundColor = Colors.White;
        ActiveButton.TextColor = (Color)Application.Current.Resources["Ink"];
        await LoadAsync();
    }

    private async void OnActiveClicked(object sender, EventArgs e)
    {
        currentStatus = "InProgress";
        ActiveButton.BackgroundColor = (Color)Application.Current!.Resources["WeleBlue"];
        ActiveButton.TextColor = Colors.White;
        AllButton.BackgroundColor = Colors.White;
        AllButton.TextColor = (Color)Application.Current.Resources["Ink"];
        await LoadAsync();
    }

    private async void OnMissionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not MissionRow mission)
        {
            return;
        }

        MissionsView.SelectedItem = null;
        await Shell.Current.GoToAsync($"{nameof(MissionDetailPage)}?missionId={mission.MissionId:D}");
    }

    private sealed record MissionRow(Guid MissionId, string Title, string Address, string Schedule, string Status, string Amount)
    {
        public static MissionRow From(ClientMissionListItemResponse item)
        {
            var title = $"{item.MissionNumber} - {item.PrestationName ?? item.ServiceName ?? "Service"}";
            var schedule = item.ScheduledFor.HasValue
                ? item.ScheduledFor.Value.ToString("dd/MM HH:mm")
                : item.CreatedAt.ToString("dd/MM HH:mm");
            var amount = item.Amount.HasValue ? $"{item.Amount:N0} {item.Currency}" : "Prix a venir";

            return new MissionRow(item.MissionId, title, item.ServiceAddress ?? "Adresse a confirmer", schedule, item.Status, amount);
        }
    }
}
