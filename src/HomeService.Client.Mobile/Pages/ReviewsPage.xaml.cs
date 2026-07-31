using System.Collections.ObjectModel;
using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

public partial class ReviewsPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
    private readonly ObservableCollection<ReviewRow> rows = [];
    public ReviewsPage() { InitializeComponent(); ReviewsView.ItemsSource = rows; }
    protected override async void OnAppearing() { base.OnAppearing(); rows.Clear(); var result = await apiClient.GetMissionsAsync("Completed"); if (result.IsSuccess && result.Response is not null) foreach (var item in result.Response) rows.Add(new(item.MissionId, item.PrestationName ?? item.ServiceName ?? "Intervention", item.MissionNumber)); EmptyState.IsVisible = rows.Count == 0; }
    private async void OnSelected(object sender, SelectionChangedEventArgs e) { if (e.CurrentSelection.FirstOrDefault() is not ReviewRow row) return; ReviewsView.SelectedItem = null; await Shell.Current.GoToAsync($"{nameof(MissionDetailPage)}?missionId={row.MissionId:D}"); }
    private async void OnBackClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");
    private sealed record ReviewRow(Guid MissionId, string Title, string MissionNumber);
}
