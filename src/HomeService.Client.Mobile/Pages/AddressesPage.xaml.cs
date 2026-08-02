using System.Collections.ObjectModel;
using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

public partial class AddressesPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
    private readonly ObservableCollection<AddressRow> rows = [];
    private readonly AddressAutocompleteSession addressAutocomplete;
    private ClientAddressResponse? editingAddress;
    private decimal? latitude;
    private decimal? longitude;
    private bool applyingAddress;
    private bool isPageActive;

    public AddressesPage()
    {
        InitializeComponent();
        addressAutocomplete = new AddressAutocompleteSession(apiClient);
        AddressesView.ItemsSource = rows;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        isPageActive = true;
        await LoadAsync();
    }

    protected override void OnDisappearing()
    {
        isPageActive = false;
        addressAutocomplete.CancelPendingSearch();
        base.OnDisappearing();
    }

    private async Task LoadAsync()
    {
        rows.Clear();
        var result = await apiClient.GetAddressesAsync();
        if (result.IsSuccess && result.Response is not null)
        {
            foreach (var item in result.Response)
            {
                rows.Add(new(item, item.IsDefault ? "Par défaut" : "›"));
            }
        }

        EmptyState.IsVisible = rows.Count == 0;
    }

    private void OnAddClicked(object sender, EventArgs e) => OpenEditor(null);

    private async void OnAddressSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not AddressRow row) return;
        AddressesView.SelectedItem = null;
        var action = await DisplayActionSheet(row.Item.Label, "Annuler", row.Item.IsDefault ? null : "Supprimer", "Modifier");
        if (action == "Modifier")
        {
            OpenEditor(row.Item);
        }
        else if (action == "Supprimer")
        {
            await apiClient.DeleteAddressAsync(row.Item.Id);
            await LoadAsync();
        }
    }

    private void OpenEditor(ClientAddressResponse? current)
    {
        editingAddress = current;
        latitude = current?.Latitude;
        longitude = current?.Longitude;
        applyingAddress = true;
        LabelEntry.Text = current?.Label ?? "Maison";
        AddressEntry.Text = current?.AddressLine ?? string.Empty;
        applyingAddress = false;
        DefaultCheckBox.IsChecked = current?.IsDefault ?? rows.Count == 0;
        EditorTitle.Text = current is null ? "Nouvelle adresse" : "Modifier l'adresse";
        EditorError.IsVisible = false;
        SuggestionsPanel.IsVisible = false;
        EditorOverlay.IsVisible = true;
        AddressEntry.Focus();
    }

    private void OnCloseEditorClicked(object sender, EventArgs e)
    {
        addressAutocomplete.CancelPendingSearch();
        EditorOverlay.IsVisible = false;
        SuggestionsPanel.IsVisible = false;
    }

    private async void OnAddressTextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            if (applyingAddress || !EditorOverlay.IsVisible) return;
            latitude = null;
            longitude = null;
            SuggestionsPanel.IsVisible = false;

            var result = await addressAutocomplete.SearchAsync(e.NewTextValue);
            if (result.IsIgnored || !isPageActive || !EditorOverlay.IsVisible || applyingAddress) return;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!isPageActive || !EditorOverlay.IsVisible || applyingAddress) return;
                SuggestionsView.ItemsSource = result.Suggestions;
                SuggestionsPanel.IsVisible = result.Suggestions.Count > 0;
                if (!string.IsNullOrWhiteSpace(result.ErrorMessage)) ShowEditorError(result.ErrorMessage);
            });
        }
        catch (Exception)
        {
            // A late autocomplete response must never close the address editor.
        }
    }

    private async void OnSuggestionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ClientAddressSuggestionResponse suggestion) return;
        SuggestionsView.SelectedItem = null;
        SuggestionsPanel.IsVisible = false;
        var details = await addressAutocomplete.ResolveAsync(suggestion);
        if (!isPageActive || !EditorOverlay.IsVisible) return;

        applyingAddress = true;
        AddressEntry.Text = details?.AddressLine ?? suggestion.FullText;
        applyingAddress = false;
        latitude = details?.Latitude;
        longitude = details?.Longitude;
    }

    private async void OnLocateClicked(object sender, EventArgs e)
    {
        LocateButton.IsEnabled = false;
        EditorError.IsVisible = false;
        try
        {
            var permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (permission != PermissionStatus.Granted)
            {
                ShowEditorError("Autorisez la localisation pour utiliser votre position actuelle.");
                return;
            }

            var location = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(12)));
            if (location is null)
            {
                ShowEditorError("Votre position n'a pas pu être détectée.");
                return;
            }

            latitude = (decimal)location.Latitude;
            longitude = (decimal)location.Longitude;
            var placemark = (await Geocoding.Default.GetPlacemarksAsync(location.Latitude, location.Longitude)).FirstOrDefault();
            applyingAddress = true;
            AddressEntry.Text = FormatPlacemark(placemark, location);
            applyingAddress = false;
        }
        catch (PermissionException)
        {
            ShowEditorError("La permission de localisation est nécessaire.");
        }
        catch (Exception)
        {
            ShowEditorError("Impossible de récupérer votre position. Vérifiez le GPS puis réessayez.");
        }
        finally
        {
            applyingAddress = false;
            LocateButton.IsEnabled = true;
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var label = LabelEntry.Text?.Trim();
        var line = AddressEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(line))
        {
            ShowEditorError("Renseignez un nom et une adresse complète.");
            return;
        }

        SaveButton.IsEnabled = false;
        EditorError.IsVisible = false;
        var request = new UpsertClientAddressRequest(label, line, latitude, longitude, DefaultCheckBox.IsChecked);
        var result = editingAddress is null
            ? await apiClient.CreateAddressAsync(request)
            : await apiClient.UpdateAddressAsync(editingAddress.Id, request);
        SaveButton.IsEnabled = true;
        if (!result.IsSuccess)
        {
            ShowEditorError(result.ErrorMessage ?? "L'adresse n'a pas pu être enregistrée.");
            return;
        }

        EditorOverlay.IsVisible = false;
        await LoadAsync();
    }

    private void ShowEditorError(string message)
    {
        EditorError.Text = message;
        EditorError.IsVisible = true;
    }

    private static string FormatPlacemark(Placemark? placemark, Location location)
    {
        if (placemark is null) return $"Position {location.Latitude:F5}, {location.Longitude:F5}";
        var street = string.Join(" ", new[] { placemark.SubThoroughfare, placemark.Thoroughfare }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var text = string.Join(", ", new[] { street, placemark.FeatureName, placemark.Locality, placemark.SubAdminArea }.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(text) ? $"Position {location.Latitude:F5}, {location.Longitude:F5}" : text;
    }

    private async void OnBackClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    private sealed record AddressRow(ClientAddressResponse Item, string Status)
    {
        public string Label => Item.Label;
        public string AddressLine => Item.AddressLine;
    }
}
