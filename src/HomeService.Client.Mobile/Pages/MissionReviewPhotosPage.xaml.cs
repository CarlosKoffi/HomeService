using System.Collections.ObjectModel;
using System.ComponentModel;
using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

[QueryProperty(nameof(MissionId), "missionId")]
public partial class MissionReviewPhotosPage : ContentPage
{
    private const int MaxPhotoCount = 4;
    private const long MaxPhotoBytes = 5 * 1024 * 1024;
    private readonly ClientMobileApiClient apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
    private readonly ClientSessionStore sessionStore = MobileServiceLocator.GetRequiredService<ClientSessionStore>();
    private readonly MissionReviewDraftStore draftStore = MobileServiceLocator.GetRequiredService<MissionReviewDraftStore>();
    private readonly ObservableCollection<ReviewPhotoSelection> selectedPhotos = [];
    private Guid currentMissionId;
    private bool isSubmitting;

    public MissionReviewPhotosPage()
    {
        InitializeComponent();
        PhotosView.ItemsSource = selectedPhotos;
        selectedPhotos.CollectionChanged += (_, _) => RefreshPhotoState();
    }

    public string? MissionId { get; set; }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!Guid.TryParse(MissionId, out currentMissionId))
        {
            ShowError("Mission introuvable.");
            SubmitButton.IsEnabled = false;
            return;
        }

        if (draftStore.Current?.MissionId != currentMissionId || !draftStore.Current.HasAllRatings)
        {
            ShowError("Revenez à l’étape précédente pour noter chaque critère.");
            SubmitButton.IsEnabled = false;
        }
    }

    private async void OnAddPhotosClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        if (selectedPhotos.Count >= MaxPhotoCount)
        {
            ShowError($"Vous pouvez ajouter {MaxPhotoCount} photos maximum.");
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
                    ShowError("La prise de photo n’est pas disponible sur cet appareil.");
                    return;
                }

                var captured = await MediaPicker.Default.CapturePhotoAsync();
                if (captured is not null)
                {
                    await AddPhotoAsync(captured);
                }

                return;
            }

            if (choice != "Choisir dans la galerie")
            {
                return;
            }

            var files = await FilePicker.Default.PickMultipleAsync(PickOptions.Images);
            foreach (var file in files.Take(MaxPhotoCount - selectedPhotos.Count))
            {
                await AddPhotoAsync(file);
            }
        }
        catch (OperationCanceledException)
        {
            // La sélection a été fermée sans photo.
        }
        catch (PermissionException)
        {
            ShowError("Autorisez Wélé à utiliser l’appareil photo et vos images dans les réglages du téléphone.");
        }
        catch (FeatureNotSupportedException)
        {
            ShowError("Cette fonction photo n’est pas disponible sur cet appareil.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ShowError("La photo n’a pas pu être ouverte.");
        }
    }

    private async Task AddPhotoAsync(FileResult file)
    {
        var selection = await ReviewPhotoSelection.FromAsync(file);
        if (selection.SizeBytes > MaxPhotoBytes)
        {
            ShowError($"{file.FileName} dépasse 5 Mo. Choisissez une photo plus légère.");
            return;
        }

        selectedPhotos.Add(selection);
    }

    private void OnRemovePhotoClicked(object sender, EventArgs e)
    {
        if (!isSubmitting && sender is Button { CommandParameter: ReviewPhotoSelection photo })
        {
            selectedPhotos.Remove(photo);
        }
    }

    private async void OnSubmitClicked(object sender, EventArgs e)
    {
        if (isSubmitting)
        {
            return;
        }

        ErrorLabel.IsVisible = false;
        var draft = draftStore.Current;
        if (currentMissionId == Guid.Empty || draft?.MissionId != currentMissionId || !draft.HasAllRatings)
        {
            ShowError("Votre notation est incomplète. Revenez à l’étape précédente.");
            return;
        }

        if (sessionStore.IsPreviewMode())
        {
            draftStore.Clear(currentMissionId);
            await Shell.Current.GoToAsync($"{nameof(MissionReviewSuccessPage)}?missionId={currentMissionId:D}");
            return;
        }

        SetSubmitting(true);
        try
        {
            var uploadedPhotos = new List<ClientMissionPhotoRequest>(selectedPhotos.Count);
            foreach (var photo in selectedPhotos)
            {
                photo.Status = "Envoi…";
                var upload = await apiClient.UploadMissionPhotoAsync(photo.File, "Photo après intervention");
                if (!upload.IsSuccess || upload.Response is null)
                {
                    photo.Status = "Échec";
                    ShowError(upload.ErrorMessage ?? "Une photo n’a pas pu être envoyée.");
                    return;
                }

                photo.Status = "Envoyée";
                uploadedPhotos.Add(new ClientMissionPhotoRequest(
                    upload.Response.OriginalFileName,
                    upload.Response.StoragePath,
                    upload.Response.ContentType,
                    upload.Response.FileSizeBytes,
                    upload.Response.Caption));
            }

            var result = await apiClient.ValidateCompletionAsync(
                currentMissionId,
                draft.QualityRating,
                draft.PunctualityRating,
                draft.PresentationRating,
                draft.PolitenessRating,
                draft.CleanlinessRating,
                draft.Comment,
                uploadedPhotos);
            if (!result.IsSuccess)
            {
                ShowError(result.ErrorMessage ?? "Votre avis n’a pas pu être envoyé.");
                return;
            }

            draftStore.Clear(currentMissionId);
            await Shell.Current.GoToAsync($"{nameof(MissionReviewSuccessPage)}?missionId={currentMissionId:D}");
        }
        finally
        {
            SetSubmitting(false);
        }
    }

    private void SetSubmitting(bool value)
    {
        isSubmitting = value;
        SubmitButton.IsEnabled = !value;
        SubmitButton.Text = value ? "Envoi en cours…" : "Envoyer mon avis";
        BusyIndicator.IsVisible = value;
        BusyIndicator.IsRunning = value;
    }

    private void RefreshPhotoState()
    {
        PhotoCountLabel.Text = $"{selectedPhotos.Count}/{MaxPhotoCount}";
        EmptyPhotoPanel.IsVisible = selectedPhotos.Count == 0;
        PhotosView.IsVisible = selectedPhotos.Count > 0;
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        if (!isSubmitting)
        {
            await Shell.Current.GoToAsync("..");
        }
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }

    private sealed class ReviewPhotoSelection : INotifyPropertyChanged
    {
        private string status = "Prête";

        private ReviewPhotoSelection(FileResult file, byte[] previewBytes)
        {
            File = file;
            PreviewBytes = previewBytes;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public FileResult File { get; }
        public string FileName => File.FileName;
        public byte[] PreviewBytes { get; }
        public long SizeBytes => PreviewBytes.LongLength;
        public ImageSource PreviewSource => ImageSource.FromStream(() => new MemoryStream(PreviewBytes, writable: false));

        public string Status
        {
            get => status;
            set
            {
                if (status == value)
                {
                    return;
                }

                status = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
            }
        }

        public static async Task<ReviewPhotoSelection> FromAsync(FileResult file)
        {
            await using var stream = await file.OpenReadAsync();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            return new ReviewPhotoSelection(file, buffer.ToArray());
        }
    }
}
