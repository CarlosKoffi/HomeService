using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;
using System.Collections.ObjectModel;

namespace HomeService.Client.Mobile.Pages;

[QueryProperty(nameof(ServiceId), "serviceId")]
[QueryProperty(nameof(PrestationId), "prestationId")]
[QueryProperty(nameof(Name), "name")]
public partial class CreateRequestPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient;
    private readonly ClientSessionStore sessionStore;
    private readonly ObservableCollection<PhotoSelection> selectedPhotos = [];
    private readonly List<ClientAddressResponse> addresses = [];
    private ClientMeResponse? client;
    private PrepareClientMissionResponse? preparation;
    private bool isPreparationLoading;
    private int maxPhotoCount = 3;
    private int currentStep = 1;

    public CreateRequestPage()
    {
        InitializeComponent();
        apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
        sessionStore = MobileServiceLocator.GetRequiredService<ClientSessionStore>();
        PhotosView.ItemsSource = selectedPhotos;
        ModePicker.SelectedIndex = 0;
        PaymentPicker.SelectedIndex = 0;
        ScheduleDatePicker.MinimumDate = DateTime.Today;
        ScheduleDatePicker.Date = DateTime.Today.AddDays(1);
        ScheduleTimePicker.Time = new TimeSpan(9, 0, 0);
    }

    public string? ServiceId { get; set; }

    public string? PrestationId { get; set; }

    public string? Name { get; set; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        TitleLabel.Text = Uri.UnescapeDataString(Name ?? "Nouvelle demande");

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
                "carlos@wele.ci");
        }

        await LoadAddressesAsync();
        await LoadPreparationAsync();
    }

    private async Task LoadPreparationAsync()
    {
        if (!Guid.TryParse(ServiceId, out var serviceId))
        {
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
            IsUrgent: IsUrgentRequested()));

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
        UrgentOptionPanel.IsVisible = preparation.UrgentOptionEnabled && ModePicker.SelectedIndex == 0;
        maxPhotoCount = Math.Max(0, preparation.MaxPhotoCount);
        TitleLabel.Text = preparation.DisplayName;
        PreparationTitleLabel.Text = preparation.DisplayName;
        PreparationDescriptionLabel.Text = string.IsNullOrWhiteSpace(preparation.Description)
            ? "Décrivez votre besoin pour recevoir une proposition adaptée."
            : preparation.Description;
        PreparationPriceLabel.Text = $"A partir de {preparation.StartingPriceAmount:N0} {preparation.Currency} - max {preparation.MaximumPriceAmount:N0} {preparation.Currency}";
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
            AddressPicker.IsVisible = true;
            AddressEmptyLabel.IsVisible = false;
            NewAddressPanel.IsVisible = false;
        }
        else if (sessionStore.IsPreviewMode())
        {
            addresses.Add(new ClientAddressResponse(Guid.Empty, "Maison", "Cocody, Riviera 3", null, null, true));
            AddressPicker.ItemsSource = addresses;
            AddressPicker.SelectedIndex = 0;
        }
        else
        {
            AddressPicker.IsVisible = false;
            SelectedAddressBorder.IsVisible = false;
            AddressEmptyLabel.IsVisible = true;
            NewAddressPanel.IsVisible = true;
            ShowNewAddressButton.IsVisible = false;
        }
    }

    private void OnAddressChanged(object sender, EventArgs e)
    {
        if (AddressPicker.SelectedItem is ClientAddressResponse address)
        {
            AddressEntry.Text = address.AddressLine;
            SelectedAddressLabel.Text = address.Label;
            SelectedAddressLineLabel.Text = address.AddressLine;
            SelectedAddressBorder.IsVisible = true;
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
            addresses.Add(new ClientAddressResponse(Guid.NewGuid(), label, addressLine, null, null, addresses.Count == 0));
        }
        else
        {
            SaveAddressButton.IsEnabled = false;
            var result = await apiClient.CreateAddressAsync(new UpsertClientAddressRequest(
                label, addressLine, null, null, addresses.Count == 0));
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
        AddressPicker.IsVisible = true;
        AddressPicker.SelectedIndex = addresses.Count - 1;
        AddressEmptyLabel.IsVisible = false;
        NewAddressPanel.IsVisible = false;
        ShowNewAddressButton.IsVisible = true;
        NewAddressLabelEntry.Text = string.Empty;
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

        if (string.IsNullOrWhiteSpace(AddressEntry.Text))
        {
            ShowError("Indiquez l'adresse de l'intervention.");
            return;
        }

        var scheduledFor = ResolveScheduledFor();
        if (ModePicker.SelectedIndex == 1 && scheduledFor <= DateTimeOffset.Now.AddMinutes(15))
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
            null,
            null,
            RequiresCompanyQuote: true,
            IsUrgent: IsUrgentRequested(),
            Photos: photoRequests);

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

        var files = await FilePicker.Default.PickMultipleAsync(PickOptions.Images);
        foreach (var file in files.Take(maxPhotoCount - selectedPhotos.Count))
        {
            selectedPhotos.Add(PhotoSelection.From(file));
        }
    }

    private void OnModeChanged(object sender, EventArgs e)
    {
        ScheduleGrid.IsVisible = ModePicker.SelectedIndex == 1;
        UrgentOptionPanel.IsVisible = preparation?.UrgentOptionEnabled == true && ModePicker.SelectedIndex == 0;
        if (ModePicker.SelectedIndex == 1)
        {
            UrgentCheckBox.IsChecked = false;
        }
        ModeHintLabel.Text = ModePicker.SelectedIndex == 0
            ? "Intervention dès que possible"
            : "Choisissez la date et l'heure";
    }

    private void OnDescriptionChanged(object sender, TextChangedEventArgs e)
    {
        DescriptionCountLabel.Text = $"{e.NewTextValue?.Length ?? 0}/250";
    }

    private void OnStepOneContinueClicked(object sender, EventArgs e)
    {
        if (isPreparationLoading)
        {
            StepOneErrorLabel.Text = "Chargement de la prestation en cours...";
            StepOneErrorLabel.IsVisible = true;
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
        if (string.IsNullOrWhiteSpace(AddressEntry.Text))
        {
            StepTwoErrorLabel.Text = "Indiquez l'adresse de l'intervention.";
            StepTwoErrorLabel.IsVisible = true;
            return;
        }

        StepTwoErrorLabel.IsVisible = false;
        SummaryServiceLabel.Text = TitleLabel.Text;
        SummaryAddressLabel.Text = AddressEntry.Text.Trim();
        SummaryScheduleLabel.Text = ModePicker.SelectedIndex == 0
            ? "Maintenant"
            : $"{ScheduleDatePicker.Date:dd/MM/yyyy} à {ScheduleTimePicker.Time:hh\\:mm}";
        SummaryDescriptionLabel.Text = string.IsNullOrWhiteSpace(DescriptionEditor.Text)
            ? "Aucune précision ajoutée."
            : DescriptionEditor.Text.Trim();
        ShowStep(3);
    }

    private void OnModifyServiceClicked(object sender, EventArgs e) => ShowStep(1);

    private void OnModifyScheduleClicked(object sender, EventArgs e) => ShowStep(2);

    private void ShowStep(int step)
    {
        currentStep = step;
        StepOnePanel.IsVisible = step == 1;
        StepTwoPanel.IsVisible = step == 2;
        StepThreePanel.IsVisible = step == 3;
        PageTitleLabel.Text = step == 3 ? string.Empty : "Nouvelle demande";
        StepTwoErrorLabel.IsVisible = false;
        ErrorLabel.IsVisible = false;
    }

    private DateTimeOffset? ResolveScheduledFor()
    {
        if (ModePicker.SelectedIndex != 1)
        {
            return null;
        }

        var date = ScheduleDatePicker.Date;
        var time = ScheduleTimePicker.Time;
        return new DateTimeOffset(date.Date.Add(time), TimeZoneInfo.Local.GetUtcOffset(date.Date.Add(time))).ToUniversalTime();
    }

    private string ResolveMissionMode()
    {
        return ModePicker.SelectedIndex == 1 ? "Scheduled" : "Instant";
    }

    private bool IsUrgentRequested()
    {
        return ModePicker.SelectedIndex == 0
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
        private PhotoSelection(FileResult file, string fileName, string sizeLabel)
        {
            File = file;
            FileName = fileName;
            SizeLabel = sizeLabel;
        }

        public FileResult File { get; }

        public string FileName { get; }

        public string SizeLabel { get; }

        public string Status { get; set; } = "Pret";

        public string? Caption { get; set; }

        public static PhotoSelection From(FileResult file)
        {
            return new PhotoSelection(file, file.FileName, "Photo selectionnee");
        }
    }
}
