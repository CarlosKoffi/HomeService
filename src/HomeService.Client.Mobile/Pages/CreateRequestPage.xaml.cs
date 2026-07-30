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
    private int maxPhotoCount = 3;

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

        var result = await apiClient.PrepareMissionAsync(new PrepareClientMissionRequest(
            serviceId,
            prestationId,
            ModePicker.SelectedIndex == 0 ? "Urgent" : "Scheduled",
            IsUrgent: ModePicker.SelectedIndex == 0));

        if (!result.IsSuccess || result.Response is null)
        {
            return;
        }

        preparation = result.Response;
        maxPhotoCount = Math.Max(0, preparation.MaxPhotoCount);
        TitleLabel.Text = preparation.DisplayName;
        PreparationTitleLabel.Text = preparation.DisplayName;
        PreparationPriceLabel.Text = $"A partir de {preparation.StartingPriceAmount:N0} {preparation.Currency} - max {preparation.MaximumPriceAmount:N0} {preparation.Currency}";
        PreparationHintLabel.Text = preparation.Message;
        PreparationIcon.Source = apiClient.ToAbsoluteMediaUrl(preparation.IconUrl);
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
        }
    }

    private void OnAddressChanged(object sender, EventArgs e)
    {
        if (AddressPicker.SelectedItem is ClientAddressResponse address)
        {
            AddressEntry.Text = address.AddressLine;
        }
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
            ModePicker.SelectedIndex == 0 ? "Urgent" : "Scheduled",
            ResolvePaymentMethod(),
            ResolveScheduledFor(),
            90,
            DescriptionEditor.Text?.Trim(),
            AddressEntry.Text.Trim(),
            null,
            null,
            RequiresCompanyQuote: true,
            IsUrgent: ModePicker.SelectedIndex == 0,
            Photos: photoRequests);

        var result = await apiClient.CreateMissionAsync(request);
        if (!result.IsSuccess || result.Response is null)
        {
            ShowError(result.ErrorMessage);
            return;
        }

        await Shell.Current.DisplayAlert("Demande envoyee", result.Response.Message, "OK");
        await Shell.Current.GoToAsync("//requests");
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
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
    }

    private DateTimeOffset? ResolveScheduledFor()
    {
        if (ModePicker.SelectedIndex != 1)
        {
            return null;
        }

        var date = ScheduleDatePicker.Date;
        var time = ScheduleTimePicker.Time;
        return new DateTimeOffset(date.Date.Add(time), TimeZoneInfo.Local.GetUtcOffset(DateTimeOffset.Now));
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
