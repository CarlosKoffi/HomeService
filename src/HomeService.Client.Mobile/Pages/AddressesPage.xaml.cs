using System.Collections.ObjectModel;
using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

public partial class AddressesPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
    private readonly ObservableCollection<AddressRow> rows = [];
    public AddressesPage() { InitializeComponent(); AddressesView.ItemsSource = rows; }
    protected override async void OnAppearing() { base.OnAppearing(); await LoadAsync(); }
    private async Task LoadAsync()
    {
        rows.Clear(); var result = await apiClient.GetAddressesAsync();
        if (result.IsSuccess && result.Response is not null)
            foreach (var item in result.Response) rows.Add(new(item, item.IsDefault ? "Par défaut" : "›"));
        EmptyState.IsVisible = rows.Count == 0;
    }
    private async void OnAddClicked(object sender, EventArgs e) => await EditAsync(null);
    private async void OnAddressSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not AddressRow row) return;
        AddressesView.SelectedItem = null;
        var action = await DisplayActionSheet(row.Item.Label, "Annuler", row.Item.IsDefault ? null : "Supprimer", "Modifier");
        if (action == "Modifier") await EditAsync(row.Item);
        else if (action == "Supprimer") { await apiClient.DeleteAddressAsync(row.Item.Id); await LoadAsync(); }
    }
    private async Task EditAsync(ClientAddressResponse? current)
    {
        var label = await DisplayPromptAsync(current is null ? "Nouvelle adresse" : "Modifier l’adresse", "Nom de l’adresse", initialValue: current?.Label ?? "Maison", maxLength: 40);
        if (string.IsNullOrWhiteSpace(label)) return;
        var line = await DisplayPromptAsync("Adresse", "Commune, quartier et précisions", initialValue: current?.AddressLine, maxLength: 160);
        if (string.IsNullOrWhiteSpace(line)) return;
        var request = new UpsertClientAddressRequest(label.Trim(), line.Trim(), current?.Latitude, current?.Longitude, current?.IsDefault ?? rows.Count == 0);
        if (current is null) await apiClient.CreateAddressAsync(request); else await apiClient.UpdateAddressAsync(current.Id, request);
        await LoadAsync();
    }
    private async void OnBackClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");
    private sealed record AddressRow(ClientAddressResponse Item, string Status)
    { public string Label => Item.Label; public string AddressLine => Item.AddressLine; }
}
