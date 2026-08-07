using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;
using HomeService.Contracts.Services;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using System.Collections.ObjectModel;
using System.Globalization;

namespace HomeService.Client.Mobile.Pages;

[QueryProperty(nameof(ServiceId), "serviceId")]
[QueryProperty(nameof(PrestationId), "prestationId")]
[QueryProperty(nameof(OptionId), "optionId")]
[QueryProperty(nameof(Name), "name")]
[QueryProperty(nameof(PreferredCompanyId), "preferredCompanyId")]
[QueryProperty(nameof(PreferredCompanyName), "preferredCompanyName")]
public partial class CreateRequestPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient;
    private readonly ClientSessionStore sessionStore;
    private readonly ObservableCollection<PhotoSelection> selectedPhotos = [];
    private readonly ObservableCollection<ServicePickerItem> availableServices = [];
    private readonly ObservableCollection<PrestationPickerItem> availablePrestations = [];
    private readonly ObservableCollection<OptionPickerItem> availableOptions = [];
    private readonly List<ClientAddressResponse> addresses = [];
    private ClientMeResponse? client;
    private PrepareClientMissionResponse? preparation;
    private ServiceSummaryResponse? selectedService;
    private bool isPreparationLoading;
    private bool autoOpenPrestationPickerPending = true;
    private int maxPhotoCount = 3;
    private int currentStep = 1;
    private decimal? currentAddressLatitude;
    private decimal? currentAddressLongitude;
    private bool isUpdatingAddressFromLocation;
    private readonly AddressAutocompleteSession addressAutocomplete;
    private bool isPageActive;
    private bool isScheduledMode;
    private DateTime selectedAppointmentDate = DateTime.UtcNow.Date.AddDays(1);
    private TimeSpan? selectedAppointmentSlotStart = TimeSpan.FromHours(8);
    private static readonly TimeSpan[] AppointmentSlotStarts = Enumerable
        .Range(0, 20)
        .Select(index => TimeSpan.FromHours(8) + TimeSpan.FromMinutes(index * 30))
        .ToArray();
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");

    public CreateRequestPage()
    {
        InitializeComponent();
        apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
        addressAutocomplete = new AddressAutocompleteSession(apiClient);
        sessionStore = MobileServiceLocator.GetRequiredService<ClientSessionStore>();
        PhotosView.ItemsSource = selectedPhotos;
        ServicePickerList.ItemsSource = availableServices;
        PrestationPickerList.ItemsSource = availablePrestations;
        OptionPickerList.ItemsSource = availableOptions;
        StepOneContinueButton.IsEnabled = false;
        PaymentPicker.SelectedIndex = 0;
        BuildAppointmentDays();
        BuildAppointmentSlots();
        UpdateModeVisualState();
        UpdateStepHeader(1);
    }

    public string? ServiceId { get; set; }

    public string? PrestationId { get; set; }

    public string? OptionId { get; set; }

    public string? Name { get; set; }

    public string? PreferredCompanyId { get; set; }

    public string? PreferredCompanyName { get; set; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        isPageActive = true;
        ResetServiceSelectionState();

        if (!sessionStore.HasSession())
        {
            await Shell.Current.GoToAsync(nameof(LoginPage));
            return;
        }

        var me = await apiClient.GetMeAsync();
        if (me.IsSuccess)
        {
            client = me.Response;
        }
        else if (sessionStore.IsPreviewMode())
        {
            client = new ClientMeResponse(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                "Carlos",
                "Konan",
                "+2250700000000",
                "carlos@wele.ci",
                null);
        }

        await LoadAddressesAsync();
        await LoadServiceCatalogAsync();
        ReorderCompanyPanel.IsVisible = Guid.TryParse(PreferredCompanyId, out _);
        ReorderCompanyLabel.Text = Uri.UnescapeDataString(PreferredCompanyName ?? "Entreprise choisie");
        if (Guid.TryParse(ServiceId, out _))
        {
            await LoadPreparationAsync(
                autoOpenOptions: Guid.TryParse(PrestationId, out _) && !Guid.TryParse(OptionId, out _));
        }
    }

    protected override void OnDisappearing()
    {
        isPageActive = false;
        addressAutocomplete.CancelPendingSearch();
        base.OnDisappearing();
    }

    private async Task LoadServiceCatalogAsync()
    {
        availableServices.Clear();
        availablePrestations.Clear();
        availableOptions.Clear();
        SelectPrestationButton.IsVisible = false;
        SelectOptionButton.IsVisible = false;

        var result = await apiClient.GetServicesAsync();
        if (!result.IsSuccess || result.Response is null)
        {
            StepOneErrorLabel.Text = result.ErrorMessage ?? "Impossible de charger les services.";
            StepOneErrorLabel.IsVisible = true;
            return;
        }

        var activeServices = result.Response
            .Where(item => item.IsActive)
            .OrderBy(item => item.Name)
            .ToList();
        var serviceItems = await Task.WhenAll(activeServices.Select(async service =>
        {
            var imageSource = await apiClient.DownloadMediaImageSourceAsync(service.IconUrl ?? service.ImageUrl);
            return ServicePickerItem.From(service, imageSource);
        }));
        foreach (var item in serviceItems)
        {
            availableServices.Add(item);
        }

        if (!Guid.TryParse(ServiceId, out var serviceId))
        {
            return;
        }

        selectedService = result.Response.FirstOrDefault(item => item.Id == serviceId && item.IsActive);
        if (selectedService is null)
        {
            ResetServiceSelectionState();
            return;
        }

        TitleLabel.Text = selectedService.Name;
        SelectServiceButton.Text = "Modifier le service";
        PrestationPickerServiceLabel.Text = selectedService.Name;

        var activePrestations = selectedService.Prestations
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToList();
        if (activePrestations.Count == 0)
        {
            autoOpenPrestationPickerPending = false;
            return;
        }

        var items = await Task.WhenAll(activePrestations.Select(async prestation =>
        {
            var imageSource = await apiClient.DownloadMediaImageSourceAsync(prestation.IllustrationUrl);
            return PrestationPickerItem.From(prestation, selectedService.Name, imageSource);
        }));
        foreach (var item in items)
        {
            availablePrestations.Add(item);
        }

        SelectPrestationButton.IsVisible = true;
        UpdatePrestationButtonText();

        if (autoOpenPrestationPickerPending && !Guid.TryParse(PrestationId, out _))
        {
            autoOpenPrestationPickerPending = false;
            OpenPrestationPicker();
        }
    }

    private void ResetServiceSelectionState()
    {
        selectedService = null;
        preparation = null;
        TitleLabel.Text = "Aucun service sélectionné";
        SelectServiceButton.Text = "Choisir un service";
        SelectPrestationButton.IsVisible = false;
        PreparationCard.IsVisible = false;
        StepOneContinueButton.IsEnabled = false;
    }

    private async Task LoadPreparationAsync(bool autoOpenOptions = false)
    {
        if (!Guid.TryParse(ServiceId, out var serviceId))
        {
            ResetServiceSelectionState();
            return;
        }

        var prestationId = Guid.TryParse(PrestationId, out var parsedPrestationId)
            ? parsedPrestationId
            : (Guid?)null;

        isPreparationLoading = true;
        StepOneErrorLabel.IsVisible = false;
        StepOneContinueButton.IsEnabled = false;
        var result = await apiClient.PrepareMissionAsync(new PrepareClientMissionRequest(
            serviceId,
            prestationId,
            ResolveMissionMode(),
            IsUrgent: IsUrgentRequested(),
            ServiceOptionId: Guid.TryParse(OptionId, out var parsedOptionId) ? parsedOptionId : null));

        if (!result.IsSuccess || result.Response is null)
        {
            if (sessionStore.IsPreviewMode())
            {
                TitleLabel.Text = Uri.UnescapeDataString(Name ?? "Service sélectionné");
                PreparationTitleLabel.Text = Uri.UnescapeDataString(Name ?? "Service sélectionné");
                PreparationPriceLabel.Text = "À partir de 15 000 FCFA - max 25 000 FCFA";
                PreparationHintLabel.Text = "Décrivez votre besoin. Une entreprise vous proposera un prix clair.";
                PreparationCard.IsVisible = true;
                PhotoHintLabel.Text = "Photos recommandées si elles aident à comprendre le besoin. Maximum 3.";
            }

            StepOneErrorLabel.Text = result.ErrorMessage ?? "Impossible de charger cette prestation.";
            StepOneErrorLabel.IsVisible = !sessionStore.IsPreviewMode();
            isPreparationLoading = false;
            StepOneContinueButton.IsEnabled = sessionStore.IsPreviewMode();

            return;
        }

        preparation = result.Response;
        LoadOptionsFromPreparation(autoOpen: autoOpenOptions);
        UrgentOptionPanel.IsVisible = preparation.UrgentOptionEnabled && !isScheduledMode;
        maxPhotoCount = Math.Max(0, preparation.MaxPhotoCount);
        TitleLabel.Text = selectedService?.Name ?? preparation.DisplayName;
        PreparationTitleLabel.Text = preparation.DisplayName;
        PreparationDescriptionLabel.Text = string.IsNullOrWhiteSpace(preparation.Description)
            ? "Décrivez votre besoin pour recevoir une proposition adaptée."
            : preparation.Description;
        PreparationPriceLabel.Text = preparation.IsFixedPrice
            ? $"Prix : {preparation.MaximumPriceAmount:N0} {preparation.Currency}"
            : $"A partir de {preparation.StartingPriceAmount:N0} {preparation.Currency} - max {preparation.MaximumPriceAmount:N0} {preparation.Currency}";
        PreparationHintLabel.Text = preparation.Message;
        PreparationIcon.Source = await apiClient.DownloadMediaImageSourceAsync(
            preparation.ImageUrl ?? preparation.IconUrl);
        PreparationCard.IsVisible = true;
        PhotoHintLabel.Text = preparation.PhotosRequired
            ? $"Photos demandees pour faciliter le devis. Maximum {maxPhotoCount}."
            : preparation.PhotosRecommended
                ? $"Photos recommandees si elles aident a comprendre le besoin. Maximum {maxPhotoCount}."
                : $"Photos facultatives. Maximum {maxPhotoCount}.";

        PaymentPicker.Items.Clear();
        foreach (var option in preparation.PaymentOptions.Where(option => option.IsAvailable))
        {
            PaymentPicker.Items.Add(option.Label);
        }

        if (PaymentPicker.Items.Count > 0)
        {
            var recommendedIndex = preparation.PaymentOptions
                .Where(option => option.IsAvailable)
                .Select((option, index) => new { option, index })
                .FirstOrDefault(item => item.option.Method == preparation.RecommendedPaymentMethod)?.index ?? 0;
            PaymentPicker.SelectedIndex = recommendedIndex;
        }

        isPreparationLoading = false;
        StepOneContinueButton.IsEnabled = true;
    }

    private void OnSelectServiceClicked(object sender, EventArgs e)
    {
        ServicePickerList.SelectedItem = null;
        ServicePickerOverlay.IsVisible = true;
    }

    private void OnCloseServicePickerClicked(object sender, EventArgs e)
    {
        ServicePickerOverlay.IsVisible = false;
        ServicePickerList.SelectedItem = null;
    }

    private async void OnServicePickerSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ServicePickerItem selected)
        {
            return;
        }

        ServiceId = selected.Id.ToString("D");
        PrestationId = null;
        OptionId = null;
        Name = selected.Name;
        ServicePickerOverlay.IsVisible = false;
        ServicePickerList.SelectedItem = null;
        StepOneErrorLabel.IsVisible = false;
        autoOpenPrestationPickerPending = true;
        await LoadServiceCatalogAsync();
        await LoadPreparationAsync();
    }

    private void LoadOptionsFromPreparation(bool autoOpen)
    {
        availableOptions.Clear();
        foreach (var option in (preparation?.AvailableOptions ?? []).OrderBy(item => item.SortOrder).ThenBy(item => item.Name))
        {
            availableOptions.Add(OptionPickerItem.From(option));
        }

        SelectOptionButton.IsVisible = availableOptions.Count > 0;
        OptionPickerPrestationLabel.Text = preparation?.ServicePrestationName ?? preparation?.DisplayName ?? string.Empty;
        UpdateOptionButtonText();
        if (autoOpen && availableOptions.Count > 0 && !Guid.TryParse(OptionId, out _))
        {
            OpenOptionPicker();
        }
    }

    private void OnSelectOptionClicked(object sender, EventArgs e) => OpenOptionPicker();

    private void OpenOptionPicker()
    {
        if (availableOptions.Count == 0) return;
        OptionPickerList.SelectedItem = null;
        OptionPickerOverlay.IsVisible = true;
    }

    private void OnCloseOptionPickerClicked(object sender, EventArgs e)
    {
        OptionPickerOverlay.IsVisible = false;
        OptionPickerList.SelectedItem = null;
    }

    private void OnOptionPickerSwiped(object sender, SwipedEventArgs e) => OnCloseOptionPickerClicked(sender, EventArgs.Empty);

    private async void OnOptionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not OptionPickerItem selected) return;
        OptionId = selected.Id.ToString("D");
        OptionPickerOverlay.IsVisible = false;
        OptionPickerList.SelectedItem = null;
        UpdateOptionButtonText();
        await LoadPreparationAsync();
    }

    private void UpdateOptionButtonText()
    {
        var selected = availableOptions.FirstOrDefault(item => Guid.TryParse(OptionId, out var id) && item.Id == id);
        SelectOptionButton.Text = selected is null ? "Selectionner une option" : $"Option : {selected.Name}  Modifier";
    }

    private void OnSelectPrestationClicked(object sender, EventArgs e)
    {
        OpenPrestationPicker();
    }

    private void OpenPrestationPicker()
    {
        if (availablePrestations.Count == 0)
        {
            return;
        }

        PrestationPickerList.SelectedItem = null;
        PrestationPickerOverlay.IsVisible = true;
    }

    private void OnClosePrestationPickerClicked(object sender, EventArgs e)
    {
        PrestationPickerOverlay.IsVisible = false;
        PrestationPickerList.SelectedItem = null;
    }

    private void OnServicePickerSwiped(object sender, SwipedEventArgs e)
    {
        OnCloseServicePickerClicked(sender, EventArgs.Empty);
    }

    private void OnPrestationPickerSwiped(object sender, SwipedEventArgs e)
    {
        OnClosePrestationPickerClicked(sender, EventArgs.Empty);
    }

    private async void OnPrestationSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not PrestationPickerItem selected)
        {
            return;
        }

        PrestationId = selected.Id.ToString("D");
        OptionId = null;
        Name = selected.Name;
        PrestationPickerOverlay.IsVisible = false;
        PrestationPickerList.SelectedItem = null;
        UpdatePrestationButtonText();
        await LoadPreparationAsync(autoOpenOptions: true);
    }

    private void UpdatePrestationButtonText()
    {
        var selected = availablePrestations.FirstOrDefault(item =>
            Guid.TryParse(PrestationId, out var prestationId) && item.Id == prestationId);
        SelectPrestationButton.Text = selected is null
            ? "Selectionner une prestation"
            : $"Prestation : {selected.Name}  Modifier";
    }

    private async Task LoadAddressesAsync()
    {
        addresses.Clear();
        AddressPicker.ItemsSource = null;
        var result = await apiClient.GetAddressesAsync();
        if (result.IsSuccess && result.Response is not null && result.Response.Count > 0)
        {
            addresses.AddRange(result.Response);
            AddressPicker.ItemsSource = addresses;
            var defaultIndex = addresses.FindIndex(item => item.IsDefault);
            AddressPicker.SelectedIndex = defaultIndex >= 0 ? defaultIndex : 0;
            AddressPicker.IsVisible = false;
            SelectedAddressChevron.IsVisible = addresses.Count > 1;
            AddressEmptyLabel.IsVisible = false;
            NewAddressPanel.IsVisible = false;
        }
        else if (sessionStore.IsPreviewMode())
        {
            addresses.Add(new ClientAddressResponse(Guid.Empty, "Maison", "Cocody, Riviera 3", 5.35995m, -4.00826m, true));
            AddressPicker.ItemsSource = addresses;
            AddressPicker.SelectedIndex = 0;
            AddressPicker.IsVisible = false;
            SelectedAddressChevron.IsVisible = false;
        }
        else
        {
            AddressPicker.IsVisible = false;
            SelectedAddressBorder.IsVisible = false;
            SelectedAddressChevron.IsVisible = false;
            AddressEmptyLabel.IsVisible = true;
            NewAddressPanel.IsVisible = true;
            ShowNewAddressButton.IsVisible = false;
        }
    }

    private async void OnAddressChanged(object sender, EventArgs e)
    {
        if (AddressPicker.SelectedItem is ClientAddressResponse address)
        {
            currentAddressLatitude = address.Latitude;
            currentAddressLongitude = address.Longitude;
            isUpdatingAddressFromLocation = true;
            AddressEntry.Text = address.AddressLine;
            isUpdatingAddressFromLocation = false;
            SelectedAddressLabel.Text = address.Label;
            SelectedAddressLineLabel.Text = address.AddressLine;
            SelectedAddressBorder.IsVisible = true;

            if (currentAddressLatitude is null || currentAddressLongitude is null)
            {
                try
                {
                    var locations = await Geocoding.Default.GetLocationsAsync(address.AddressLine);
                    var location = locations.FirstOrDefault();
                    if (location is not null)
                    {
                        currentAddressLatitude = (decimal)location.Latitude;
                        currentAddressLongitude = (decimal)location.Longitude;
                    }
                }
                catch
                {
                    // The address remains usable even if the optional map lookup fails.
                }
            }

            UpdateAddressMap(address.Label, address.AddressLine);
        }
    }

    private async void OnSelectedAddressTapped(object sender, TappedEventArgs e)
    {
        if (addresses.Count <= 1)
        {
            return;
        }

        var choices = addresses
            .Select((address, index) => $"{index + 1}. {address.Label} — {address.AddressLine}")
            .ToArray();
        var selected = await DisplayActionSheet("Choisir une adresse", "Annuler", null, choices);
        var selectedIndex = Array.IndexOf(choices, selected);
        if (selectedIndex >= 0)
        {
            AddressPicker.SelectedIndex = selectedIndex;
        }
    }

    private void OnShowNewAddressClicked(object sender, EventArgs e)
    {
        NewAddressPanel.IsVisible = !NewAddressPanel.IsVisible;
        if (NewAddressPanel.IsVisible)
        {
            NewAddressLabelEntry.Focus();
        }
    }

    private async void OnAddressTextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            if (isUpdatingAddressFromLocation)
            {
                return;
            }

            currentAddressLatitude = null;
            currentAddressLongitude = null;
            AddressSuggestionsPanel.IsVisible = false;

            var query = e.NewTextValue?.Trim();
            if (string.IsNullOrWhiteSpace(query) || query.Length < 3 || sessionStore.IsPreviewMode())
            {
                addressAutocomplete.CancelPendingSearch();
                return;
            }

            var result = await addressAutocomplete.SearchAsync(query);
            if (result.IsIgnored || !isPageActive || isUpdatingAddressFromLocation)
            {
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!isPageActive || isUpdatingAddressFromLocation) return;
                AddressSuggestionsView.ItemsSource = result.Suggestions;
                AddressSuggestionsPanel.IsVisible = result.Suggestions.Count > 0;
                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    ShowAddressError(result.ErrorMessage);
                }
            });
        }
        catch (Exception)
        {
            // Autocomplete is optional and must never interrupt a mission request.
        }
    }

    private async void OnAddressSuggestionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ClientAddressSuggestionResponse suggestion)
        {
            return;
        }

        AddressSuggestionsView.SelectedItem = null;
        AddressSuggestionsPanel.IsVisible = false;
        var details = await addressAutocomplete.ResolveAsync(suggestion);
        if (!isPageActive)
        {
            return;
        }

        if (details is null)
        {
            isUpdatingAddressFromLocation = true;
            AddressEntry.Text = suggestion.FullText;
            isUpdatingAddressFromLocation = false;
            return;
        }

        isUpdatingAddressFromLocation = true;
        AddressEntry.Text = details.AddressLine;
        isUpdatingAddressFromLocation = false;
        currentAddressLatitude = details.Latitude;
        currentAddressLongitude = details.Longitude;
        UpdateAddressMap(NewAddressLabelEntry.Text?.Trim() ?? "Nouvelle adresse", details.AddressLine);
    }

    private async void OnLocateAddressClicked(object sender, EventArgs e)
    {
        StepTwoErrorLabel.IsVisible = false;
        LocateAddressButton.IsEnabled = false;

        try
        {
            var permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (permission != PermissionStatus.Granted)
            {
                ShowAddressError("Autorisez la localisation pour utiliser votre position actuelle.");
                return;
            }

            var location = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(
                GeolocationAccuracy.Medium,
                TimeSpan.FromSeconds(12)));
            if (location is null)
            {
                ShowAddressError("Votre position n'a pas pu être détectée. Saisissez l'adresse manuellement.");
                return;
            }

            currentAddressLatitude = (decimal)location.Latitude;
            currentAddressLongitude = (decimal)location.Longitude;
            var placemarks = await Geocoding.Default.GetPlacemarksAsync(location.Latitude, location.Longitude);
            var placemark = placemarks.FirstOrDefault();
            var address = FormatPlacemark(placemark);

            isUpdatingAddressFromLocation = true;
            AddressEntry.Text = string.IsNullOrWhiteSpace(address)
                ? $"Position {location.Latitude:F5}, {location.Longitude:F5}"
                : address;
            isUpdatingAddressFromLocation = false;

            if (string.IsNullOrWhiteSpace(NewAddressLabelEntry.Text))
            {
                NewAddressLabelEntry.Text = "Ma position";
            }

            UpdateAddressMap(NewAddressLabelEntry.Text.Trim(), AddressEntry.Text);
        }
        catch (FeatureNotSupportedException)
        {
            ShowAddressError("La localisation n'est pas disponible sur cet appareil.");
        }
        catch (PermissionException)
        {
            ShowAddressError("La permission de localisation est nécessaire.");
        }
        catch
        {
            ShowAddressError("Impossible de récupérer votre position. Vérifiez le GPS puis réessayez.");
        }
        finally
        {
            isUpdatingAddressFromLocation = false;
            LocateAddressButton.IsEnabled = true;
        }
    }

    private void ShowAddressError(string message)
    {
        StepTwoErrorLabel.Text = message;
        StepTwoErrorLabel.IsVisible = true;
    }

    private static string FormatPlacemark(Placemark? placemark)
    {
        if (placemark is null)
        {
            return string.Empty;
        }

        var street = string.Join(" ", new[] { placemark.SubThoroughfare, placemark.Thoroughfare }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.Join(", ", new[] { street, placemark.FeatureName, placemark.Locality, placemark.SubAdminArea }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private async void OnSaveAddressClicked(object sender, EventArgs e)
    {
        StepTwoErrorLabel.IsVisible = false;
        var label = NewAddressLabelEntry.Text?.Trim();
        var addressLine = AddressEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(addressLine))
        {
            StepTwoErrorLabel.Text = "Renseignez un nom et l'adresse complète.";
            StepTwoErrorLabel.IsVisible = true;
            return;
        }

        if (sessionStore.IsPreviewMode())
        {
            addresses.Add(new ClientAddressResponse(
                Guid.NewGuid(), label, addressLine, currentAddressLatitude, currentAddressLongitude, addresses.Count == 0));
        }
        else
        {
            SaveAddressButton.IsEnabled = false;
            var result = await apiClient.CreateAddressAsync(new UpsertClientAddressRequest(
                label, addressLine, currentAddressLatitude, currentAddressLongitude, addresses.Count == 0));
            SaveAddressButton.IsEnabled = true;
            if (!result.IsSuccess || result.Response is null)
            {
                StepTwoErrorLabel.Text = result.ErrorMessage ?? "L'adresse n'a pas pu être enregistrée.";
                StepTwoErrorLabel.IsVisible = true;
                return;
            }

            addresses.Add(result.Response);
        }

        AddressPicker.ItemsSource = null;
        AddressPicker.ItemsSource = addresses;
        AddressPicker.IsVisible = false;
        AddressPicker.SelectedIndex = addresses.Count - 1;
        SelectedAddressChevron.IsVisible = addresses.Count > 1;
        AddressEmptyLabel.IsVisible = false;
        NewAddressPanel.IsVisible = false;
        ShowNewAddressButton.IsVisible = true;
        NewAddressLabelEntry.Text = string.Empty;
    }

    private void UpdateAddressMap(string? label, string? addressLine)
    {
        if (currentAddressLatitude is null || currentAddressLongitude is null)
        {
            AddressMapBorder.IsVisible = false;
            return;
        }

        var location = new Microsoft.Maui.Devices.Sensors.Location(
            (double)currentAddressLatitude.Value,
            (double)currentAddressLongitude.Value);
        AddressMap.Pins.Clear();
        AddressMap.Pins.Add(new Pin
        {
            Label = string.IsNullOrWhiteSpace(label) ? "Adresse d'intervention" : label,
            Address = addressLine ?? string.Empty,
            Location = location,
            Type = PinType.Place
        });
        AddressMap.MoveToRegion(MapSpan.FromCenterAndRadius(location, Distance.FromKilometers(0.65)));
#if !WINDOWS
        AddressMapBorder.IsVisible = true;
#endif
    }

    private async void OnCreateClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        if (client is null)
        {
            ShowError("Connectez-vous avant de creer une demande.");
            return;
        }

        if (!Guid.TryParse(ServiceId, out var serviceId))
        {
            ShowError("Service invalide.");
            return;
        }

        Guid? prestationId = Guid.TryParse(PrestationId, out var parsedPrestationId)
            ? parsedPrestationId
            : null;
        Guid? optionId = Guid.TryParse(OptionId, out var parsedOptionId) ? parsedOptionId : null;

        if (availableOptions.Count > 0 && optionId is null)
        {
            ShowError("Choisissez une option avant de continuer.");
            OpenOptionPicker();
            return;
        }

        if (string.IsNullOrWhiteSpace(AddressEntry.Text))
        {
            ShowError("Indiquez l'adresse de l'intervention.");
            return;
        }

        var scheduledFor = ResolveScheduledFor();
        if (isScheduledMode && scheduledFor <= DateTimeOffset.Now.AddMinutes(15))
        {
            ShowError("Choisissez un rendez-vous au moins 15 minutes dans le futur.");
            return;
        }

        if (sessionStore.IsPreviewMode())
        {
            await Shell.Current.DisplayAlert("Demande envoyée", "Aperçu : la demande est simulée et visible dans Mes demandes.", "OK");
            await Shell.Current.GoToAsync("//requests");
            return;
        }

        var photoRequests = new List<ClientMissionPhotoRequest>();
        foreach (var photo in selectedPhotos)
        {
            photo.Status = "Envoi...";
            var upload = await apiClient.UploadMissionPhotoAsync(photo.File, photo.Caption);
            if (!upload.IsSuccess || upload.Response is null)
            {
                photo.Status = "Erreur";
                ShowError(upload.ErrorMessage ?? "Une photo n'a pas pu etre envoyee.");
                return;
            }

            photo.Status = "OK";
            photoRequests.Add(new ClientMissionPhotoRequest(
                upload.Response.OriginalFileName,
                upload.Response.StoragePath,
                upload.Response.ContentType,
                upload.Response.FileSizeBytes,
                upload.Response.Caption));
        }

        var request = new CreateClientMissionRequest(
            client.FirstName,
            client.LastName,
            client.PhoneNumber,
            serviceId,
            prestationId,
            ResolveMissionMode(),
            ResolvePaymentMethod(),
            scheduledFor,
            90,
            DescriptionEditor.Text?.Trim(),
            AddressEntry.Text.Trim(),
            currentAddressLatitude,
            currentAddressLongitude,
            RequiresCompanyQuote: true,
            IsUrgent: IsUrgentRequested(),
            Photos: photoRequests,
            ServiceOptionId: optionId,
            PreferredCompanyId: Guid.TryParse(PreferredCompanyId, out var preferredCompanyId)
                ? preferredCompanyId
                : null);

        var result = await apiClient.CreateMissionAsync(request);
        if (!result.IsSuccess || result.Response is null)
        {
            ShowError(result.ErrorMessage);
            return;
        }

        await ContinueToPaymentAsync(result.Response.MissionId, result.Response.Message);
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        if (OptionPickerOverlay.IsVisible)
        {
            OnCloseOptionPickerClicked(sender, e);
            return;
        }

        if (ServicePickerOverlay.IsVisible)
        {
            OnCloseServicePickerClicked(sender, e);
            return;
        }

        if (PrestationPickerOverlay.IsVisible)
        {
            OnClosePrestationPickerClicked(sender, e);
            return;
        }

        if (currentStep > 1)
        {
            ShowStep(currentStep - 1);
            return;
        }

        await Shell.Current.GoToAsync("..");
    }

    private async void OnAddPhotosClicked(object sender, EventArgs e)
    {
        if (selectedPhotos.Count >= maxPhotoCount)
        {
            ShowError($"Ajoutez {maxPhotoCount} photo(s) maximum pour garder la demande legere.");
            return;
        }

        try
        {
            var choice = await DisplayActionSheet(
                "Ajouter une photo",
                "Annuler",
                null,
                "Prendre une photo",
                "Choisir dans la galerie");

            if (choice == "Prendre une photo")
            {
                if (!MediaPicker.Default.IsCaptureSupported)
                {
                    ShowError("La prise de photo n'est pas disponible sur cet appareil.");
                    return;
                }

                var capturedPhoto = await MediaPicker.Default.CapturePhotoAsync();
                if (capturedPhoto is not null)
                {
                    selectedPhotos.Add(await PhotoSelection.FromAsync(capturedPhoto));
                }

                return;
            }

            if (choice != "Choisir dans la galerie")
            {
                return;
            }

            var files = await FilePicker.Default.PickMultipleAsync(PickOptions.Images);
            foreach (var file in files.Take(maxPhotoCount - selectedPhotos.Count))
            {
                selectedPhotos.Add(await PhotoSelection.FromAsync(file));
            }
        }
        catch (OperationCanceledException)
        {
            // The user closed the camera or gallery without selecting a photo.
        }
        catch (PermissionException)
        {
            ShowError("Autorisez Wele a utiliser l'appareil photo et vos images dans les reglages du telephone.");
        }
        catch (FeatureNotSupportedException)
        {
            ShowError("Cette fonction photo n'est pas disponible sur cet appareil.");
        }
        catch
        {
            ShowError("La photo n'a pas pu etre ajoutee. Reessayez avec une image JPG ou PNG.");
        }
    }

    private void OnRemovePhotoClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: PhotoSelection photo })
        {
            selectedPhotos.Remove(photo);
        }
    }

    private void OnInstantModeTapped(object sender, TappedEventArgs e)
    {
        isScheduledMode = false;
        UpdateModeVisualState();
    }

    private void OnScheduledModeTapped(object sender, TappedEventArgs e)
    {
        isScheduledMode = true;
        UrgentCheckBox.IsChecked = false;
        BuildAppointmentDays();
        BuildAppointmentSlots();
        UpdateModeVisualState();
    }

    private void UpdateModeVisualState()
    {
        var blue = Color.FromArgb("#155EEF");
        var selectedBackground = Color.FromArgb("#EEF4FF");
        var selectedStroke = Color.FromArgb("#BFD1F0");
        var idleStroke = Color.FromArgb("#DCE1E8");
        var ink = Color.FromArgb("#111827");

        InstantModeCard.BackgroundColor = isScheduledMode ? Colors.White : selectedBackground;
        InstantModeCard.Stroke = isScheduledMode ? idleStroke : selectedStroke;
        InstantModeTitle.TextColor = isScheduledMode ? ink : blue;
        InstantModeCheck.BackgroundColor = isScheduledMode ? Colors.White : blue;
        InstantModeCheck.Stroke = isScheduledMode ? Color.FromArgb("#C8D0DC") : blue;
        InstantModeCheckLabel.IsVisible = !isScheduledMode;

        ScheduledModeCard.BackgroundColor = isScheduledMode ? selectedBackground : Colors.White;
        ScheduledModeCard.Stroke = isScheduledMode ? selectedStroke : idleStroke;
        ScheduledModeTitle.TextColor = isScheduledMode ? blue : ink;
        ScheduledModeCheck.BackgroundColor = isScheduledMode ? blue : Colors.White;
        ScheduledModeCheck.Stroke = isScheduledMode ? blue : Color.FromArgb("#C8D0DC");
        ScheduledModeCheckLabel.IsVisible = isScheduledMode;

        ScheduleGrid.IsVisible = isScheduledMode;
        UrgentOptionPanel.IsVisible = preparation?.UrgentOptionEnabled == true && !isScheduledMode;
    }

    private void BuildAppointmentDays()
    {
        AppointmentDaysLayout.Clear();
        for (var dayOffset = 0; dayOffset < 7; dayOffset++)
        {
            var date = DateTime.UtcNow.Date.AddDays(dayOffset);
            var isSelected = date.Date == selectedAppointmentDate.Date;
            var dayLabel = date.ToString("ddd", FrenchCulture).TrimEnd('.').ToUpperInvariant();
            var textColor = isSelected ? Color.FromArgb("#155EEF") : Color.FromArgb("#526076");

            var content = new VerticalStackLayout
            {
                Spacing = 2,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label
                    {
                        Text = dayOffset == 0 ? "AUJ." : dayLabel,
                        FontSize = 9,
                        TextColor = textColor,
                        HorizontalTextAlignment = TextAlignment.Center
                    },
                    new Label
                    {
                        Text = date.Day.ToString(CultureInfo.InvariantCulture),
                        FontSize = 15,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = textColor,
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                }
            };
            var card = new Border
            {
                HeightRequest = 58,
                WidthRequest = 56,
                Padding = 4,
                BackgroundColor = isSelected ? Color.FromArgb("#EEF4FF") : Colors.White,
                Stroke = isSelected ? Color.FromArgb("#BFD1F0") : Color.FromArgb("#DCE1E8"),
                StrokeThickness = 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                Content = content
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                selectedAppointmentDate = date;
                BuildAppointmentDays();
                BuildAppointmentSlots();
            };
            card.GestureRecognizers.Add(tap);
            AppointmentDaysLayout.Add(card);
        }
    }

    private void BuildAppointmentSlots()
    {
        AppointmentSlotsLayout.Clear();
        var availableSlots = AppointmentSlotStarts
            .Where(slot => ToScheduledDateTimeOffset(selectedAppointmentDate, slot) > DateTimeOffset.Now.AddMinutes(15))
            .ToArray();

        if (selectedAppointmentSlotStart is null || !availableSlots.Contains(selectedAppointmentSlotStart.Value))
        {
            selectedAppointmentSlotStart = availableSlots.FirstOrDefault();
            if (availableSlots.Length == 0)
            {
                selectedAppointmentSlotStart = null;
            }
        }

        if (availableSlots.Length == 0)
        {
            AppointmentSlotsLayout.Add(new Label
            {
                Text = "Aucun créneau restant ce jour. Choisissez le jour suivant.",
                FontSize = 11,
                TextColor = Color.FromArgb("#687386")
            });
            return;
        }

        foreach (var slot in availableSlots)
        {
            var isSelected = slot == selectedAppointmentSlotStart;
            var button = new Button
            {
                Text = $"{FormatSlotTime(slot)} - {FormatSlotTime(slot + TimeSpan.FromMinutes(30))}",
                CommandParameter = slot,
                HeightRequest = 40,
                MinimumHeightRequest = 40,
                WidthRequest = 112,
                Padding = new Thickness(8, 0),
                FontSize = 10,
                CornerRadius = 9,
                BackgroundColor = isSelected ? Color.FromArgb("#155EEF") : Colors.White,
                TextColor = isSelected ? Colors.White : Color.FromArgb("#111827"),
                BorderColor = isSelected ? Color.FromArgb("#155EEF") : Color.FromArgb("#DCE1E8"),
                BorderWidth = 1
            };
            button.Clicked += OnAppointmentSlotClicked;
            AppointmentSlotsLayout.Add(button);
        }
    }

    private void OnAppointmentSlotClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: TimeSpan slot })
        {
            selectedAppointmentSlotStart = slot;
            BuildAppointmentSlots();
        }
    }

    private void OnDescriptionChanged(object sender, TextChangedEventArgs e)
    {
        DescriptionCountLabel.Text = $"{e.NewTextValue?.Length ?? 0}/250";
    }

    private void OnStepOneContinueClicked(object sender, EventArgs e)
    {
        if (!Guid.TryParse(ServiceId, out _))
        {
            StepOneErrorLabel.Text = "Choisissez d'abord un service.";
            StepOneErrorLabel.IsVisible = true;
            return;
        }

        if (isPreparationLoading)
        {
            StepOneErrorLabel.Text = "Chargement de la prestation en cours...";
            StepOneErrorLabel.IsVisible = true;
            return;
        }

        if (availableOptions.Count > 0 && !Guid.TryParse(OptionId, out _))
        {
            StepOneErrorLabel.Text = "Choisissez une option pour cette prestation.";
            StepOneErrorLabel.IsVisible = true;
            OpenOptionPicker();
            return;
        }

        if (preparation is null && !sessionStore.IsPreviewMode())
        {
            StepOneErrorLabel.Text = "La prestation n'a pas pu être chargée. Revenez en arrière puis réessayez.";
            StepOneErrorLabel.IsVisible = true;
            return;
        }

        StepOneErrorLabel.IsVisible = false;
        ShowStep(2);
    }

    private void OnStepTwoContinueClicked(object sender, EventArgs e)
    {
        if (isScheduledMode && selectedAppointmentSlotStart is null)
        {
            StepTwoErrorLabel.Text = "Choisissez un créneau d'arrivée.";
            StepTwoErrorLabel.IsVisible = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(AddressEntry.Text))
        {
            StepTwoErrorLabel.Text = "Indiquez l'adresse de l'intervention.";
            StepTwoErrorLabel.IsVisible = true;
            return;
        }

        StepTwoErrorLabel.IsVisible = false;
        SummaryServiceLabel.Text = selectedService?.Name ?? preparation?.ServiceName ?? TitleLabel.Text;
        SummaryPrestationLabel.Text = preparation?.ServicePrestationName ?? string.Empty;
        SummaryPrestationPanel.IsVisible = !string.IsNullOrWhiteSpace(SummaryPrestationLabel.Text);
        SummaryOptionLabel.Text = preparation?.ServiceOptionName ?? string.Empty;
        SummaryOptionPanel.IsVisible = !string.IsNullOrWhiteSpace(SummaryOptionLabel.Text);
        UpdateSummaryPrice();
        SummaryAddressLabel.Text = AddressEntry.Text.Trim();
        SummaryScheduleLabel.Text = isScheduledMode
            ? BuildAppointmentSummary()
            : "Dès que possible";
        SummaryDescriptionLabel.Text = string.IsNullOrWhiteSpace(DescriptionEditor.Text)
            ? "Aucune précision ajoutée."
            : DescriptionEditor.Text.Trim();
        ShowStep(3);
    }

    private void UpdateSummaryPrice()
    {
        if (preparation is null)
        {
            SummaryPricePanel.IsVisible = false;
            return;
        }

        SummaryPricePanel.IsVisible = true;
        SummaryPriceLabel.Text = preparation.IsFixedPrice
            ? $"{preparation.MaximumPriceAmount:N0} {preparation.Currency}"
            : $"{preparation.StartingPriceAmount:N0} - {preparation.MaximumPriceAmount:N0} {preparation.Currency}";
        SummaryPriceHintLabel.Text = preparation.IsFixedPrice
            ? "Prix fixe"
            : "L'entreprise confirmera le prix final avant paiement.";
    }

    private void OnModifyServiceClicked(object sender, EventArgs e) => ShowStep(1);

    private void OnModifyScheduleClicked(object sender, EventArgs e) => ShowStep(2);

    private void ShowStep(int step)
    {
        currentStep = step;
        StepOnePanel.IsVisible = step == 1;
        StepTwoPanel.IsVisible = step == 2;
        StepThreePanel.IsVisible = step == 3;
        PageTitleLabel.Text = "Nouvelle demande";
        UpdateStepHeader(step);
        StepTwoErrorLabel.IsVisible = false;
        ErrorLabel.IsVisible = false;
    }

    private void UpdateStepHeader(int step)
    {
        SetStepIndicator(StepOneCircle, StepOneCircleLabel, 1, step);
        SetStepIndicator(StepTwoCircle, StepTwoCircleLabel, 2, step);
        SetStepIndicator(StepThreeCircle, StepThreeCircleLabel, 3, step);
    }

    private static void SetStepIndicator(Border circle, Label label, int indicatorStep, int activeStep)
    {
        var isCompleted = indicatorStep < activeStep;
        var isActive = indicatorStep == activeStep;
        circle.BackgroundColor = isActive || isCompleted
            ? Color.FromArgb("#155EEF")
            : Color.FromArgb("#F0F2F6");
        label.Text = isCompleted ? "✓" : indicatorStep.ToString(CultureInfo.InvariantCulture);
        label.TextColor = isActive || isCompleted ? Colors.White : Color.FromArgb("#687386");
    }

    private DateTimeOffset? ResolveScheduledFor()
    {
        if (!isScheduledMode || selectedAppointmentSlotStart is null)
        {
            return null;
        }

        return ToScheduledDateTimeOffset(selectedAppointmentDate, selectedAppointmentSlotStart.Value).ToUniversalTime();
    }

    private static DateTimeOffset ToScheduledDateTimeOffset(DateTime date, TimeSpan time)
    {
        // Abidjan uses UTC all year, so the selected local slot can be stored directly in UTC.
        var abidjanDateTime = DateTime.SpecifyKind(date.Date.Add(time), DateTimeKind.Unspecified);
        return new DateTimeOffset(abidjanDateTime, TimeSpan.Zero);
    }

    private string BuildAppointmentSummary()
    {
        if (selectedAppointmentSlotStart is null)
        {
            return "Créneau à sélectionner";
        }

        var end = selectedAppointmentSlotStart.Value + TimeSpan.FromMinutes(30);
        var day = selectedAppointmentDate.ToString("dddd d MMMM", FrenchCulture);
        return $"{FrenchCulture.TextInfo.ToTitleCase(day)} · arrivée entre {FormatSlotTime(selectedAppointmentSlotStart.Value)} et {FormatSlotTime(end)}";
    }

    private static string FormatSlotTime(TimeSpan time)
    {
        return $"{time.Hours:00}h{time.Minutes:00}";
    }

    private string ResolveMissionMode()
    {
        return isScheduledMode ? "Scheduled" : "Instant";
    }

    private bool IsUrgentRequested()
    {
        return !isScheduledMode
            && preparation?.UrgentOptionEnabled == true
            && UrgentCheckBox.IsChecked;
    }

    private async Task ContinueToPaymentAsync(Guid missionId, string message)
    {
        var methodsResult = await apiClient.GetPaymentMethodsAsync();
        if (!methodsResult.IsSuccess || methodsResult.Response is null)
        {
            await Shell.Current.DisplayAlert("Demande envoyee", message, "OK");
            await Shell.Current.GoToAsync($"{nameof(PaymentMethodsPage)}?missionId={missionId:D}");
            return;
        }

        var methods = methodsResult.Response.Where(item => item.IsActive).ToList();
        if (methods.Count == 1)
        {
            var selection = await apiClient.SelectMissionPaymentMethodAsync(missionId, methods[0].Id);
            if (selection.IsSuccess)
            {
                await Shell.Current.DisplayAlert("Demande envoyee", $"{message}\n\nPaiement : {methods[0].Label}", "OK");
                await Shell.Current.GoToAsync($"{nameof(MissionDetailPage)}?missionId={missionId:D}");
                return;
            }
        }

        var route = methods.Count == 0 ? nameof(AddPaymentMethodPage) : nameof(PaymentMethodsPage);
        await Shell.Current.DisplayAlert(
            "Demande envoyee",
            methods.Count == 0
                ? $"{message}\n\nAjoutez maintenant un moyen de paiement pour pouvoir accepter le prix plus tard."
                : $"{message}\n\nChoisissez le moyen de paiement a utiliser pour cette demande.",
            "Continuer");
        await Shell.Current.GoToAsync($"{route}?missionId={missionId:D}");
    }

    private string ResolvePaymentMethod()
    {
        if (preparation is null || PaymentPicker.SelectedIndex < 0)
        {
            return PaymentPicker.SelectedIndex == 0 ? "MobileMoney" : "Card";
        }

        return preparation.PaymentOptions
            .Where(option => option.IsAvailable)
            .ElementAtOrDefault(PaymentPicker.SelectedIndex)?.Method ?? preparation.RecommendedPaymentMethod;
    }

    private void ShowError(string? message)
    {
        ErrorLabel.Text = message ?? "Demande impossible.";
        ErrorLabel.IsVisible = true;
    }

    private sealed class PhotoSelection
    {
        private PhotoSelection(FileResult file, string fileName, string sizeLabel, byte[] previewBytes)
        {
            File = file;
            FileName = fileName;
            SizeLabel = sizeLabel;
            PreviewBytes = previewBytes;
        }

        public FileResult File { get; }

        public string FileName { get; }

        public string SizeLabel { get; }

        public byte[] PreviewBytes { get; }

        public ImageSource PreviewSource => ImageSource.FromStream(() => new MemoryStream(PreviewBytes));

        public string Status { get; set; } = "Pret";

        public string? Caption { get; set; }

        public static async Task<PhotoSelection> FromAsync(FileResult file)
        {
            await using var stream = await file.OpenReadAsync();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            return new PhotoSelection(file, file.FileName, "Photo selectionnee", buffer.ToArray());
        }
    }

    private sealed record PrestationPickerItem(
        Guid Id,
        string Name,
        string Description,
        string ServiceName,
        string PriceLabel,
        ImageSource? ImageSource,
        string Initials,
        bool ShowInitials)
    {
        public static PrestationPickerItem From(
            ServicePrestationSummaryResponse prestation,
            string serviceName,
            ImageSource? imageSource)
        {
            var minimum = prestation.PriceMinAmount ?? prestation.NormalPriceAmount;
            var maximum = prestation.PriceMaxAmount ?? prestation.PremiumPriceAmount;
            var priceLabel = maximum > minimum
                ? $"{minimum:N0} - {maximum:N0} {prestation.Currency}"
                : $"A partir de {minimum:N0} {prestation.Currency}";
            var initials = string.Concat(prestation.Name
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(2)
                .Select(word => char.ToUpperInvariant(word[0])));

            return new PrestationPickerItem(
                prestation.Id,
                prestation.Name,
                string.IsNullOrWhiteSpace(prestation.Description) ? serviceName : prestation.Description,
                serviceName,
                priceLabel,
                imageSource,
                initials,
                imageSource is null);
        }
    }

    private sealed record OptionPickerItem(Guid Id, string Name, string Description, string PriceLabel)
    {
        public static OptionPickerItem From(HomeService.Contracts.Clients.ServiceOptionSummaryResponse option)
        {
            var price = option.IsFixedPrice || option.PriceMinAmount == option.PriceMaxAmount
                ? $"Prix fixe : {option.PriceMaxAmount:N0} {option.Currency}"
                : $"{option.PriceMinAmount:N0} - {option.PriceMaxAmount:N0} {option.Currency}";
            return new OptionPickerItem(option.Id, option.Name, option.Description ?? "Option de la prestation", price);
        }
    }

    private sealed record ServicePickerItem(
        Guid Id,
        string Name,
        string Description,
        string PriceLabel,
        ImageSource? ImageSource,
        string Initials,
        bool ShowInitials)
    {
        public static ServicePickerItem From(ServiceSummaryResponse service, ImageSource? imageSource)
        {
            var minimum = service.PriceMinAmount ?? service.NormalPriceAmount;
            var maximum = service.PriceMaxAmount ?? service.PremiumPriceAmount;
            var price = maximum > minimum
                ? $"De {minimum:N0} à {maximum:N0} {service.Currency}"
                : $"À partir de {minimum:N0} {service.Currency}";
            var initials = string.Concat(service.Name
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(2)
                .Select(word => char.ToUpperInvariant(word[0])));
            return new ServicePickerItem(
                service.Id,
                service.Name,
                string.IsNullOrWhiteSpace(service.Description) ? "Service à domicile" : service.Description,
                price,
                imageSource,
                initials,
                imageSource is null);
        }
    }
}
