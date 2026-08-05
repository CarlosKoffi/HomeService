using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

public partial class ProfileInformationPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
    private readonly ClientSessionStore sessionStore = MobileServiceLocator.GetRequiredService<ClientSessionStore>();
    private bool isLoading;
    private bool isUploadingPhoto;

    public ProfileInformationPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (isLoading)
        {
            return;
        }

        isLoading = true;
        try
        {
            var result = await apiClient.GetMeAsync();
            if (result.IsSuccess && result.Response is not null)
            {
                FirstNameEntry.Text = result.Response.FirstName;
                LastNameEntry.Text = result.Response.LastName;
                PhoneEntry.Text = result.Response.PhoneNumber;
                EmailEntry.Text = result.Response.Email;
                await LoadProfilePhotoAsync(result.Response.ProfilePhotoUrl);
                ErrorLabel.IsVisible = false;
            }
            else
            {
                ShowError(result.ErrorMessage ?? "Impossible de charger vos informations.");
            }
        }
        catch (Exception)
        {
            ShowError("Vos informations ne peuvent pas être chargées pour le moment.");
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task LoadProfilePhotoAsync(string? photoUrl)
    {
        if (string.IsNullOrWhiteSpace(photoUrl))
        {
            ProfilePhotoImage.Source = "nav_profile.svg";
            return;
        }

        try
        {
            var image = await apiClient.DownloadProfilePhotoAsync(photoUrl);
            if (image is not null)
            {
                ProfilePhotoImage.Source = image;
            }
        }
        catch (Exception)
        {
            ProfilePhotoImage.Source = "nav_profile.svg";
        }
    }

    private async void OnProfilePhotoTapped(object sender, TappedEventArgs e) => await ChooseProfilePhotoAsync();

    private async void OnChangePhotoClicked(object sender, EventArgs e) => await ChooseProfilePhotoAsync();

    private async Task ChooseProfilePhotoAsync()
    {
        if (isUploadingPhoto)
        {
            return;
        }

        if (sessionStore.IsPreviewMode())
        {
            ShowPhotoStatus("La photo n'est pas modifiable en mode aperçu.", isError: true);
            return;
        }

        var choice = await DisplayActionSheet(
            "Photo de profil",
            "Annuler",
            null,
            "Prendre une photo",
            "Choisir dans la galerie");

        FileResult? file = null;
        try
        {
            file = choice switch
            {
                "Prendre une photo" => await MediaPicker.Default.CapturePhotoAsync(),
                "Choisir dans la galerie" => await MediaPicker.Default.PickPhotoAsync(),
                _ => null
            };
        }
        catch (FeatureNotSupportedException)
        {
            ShowPhotoStatus("La caméra ou la galerie n'est pas disponible sur cet appareil.", isError: true);
        }
        catch (PermissionException)
        {
            ShowPhotoStatus("Autorisez l'accès aux photos ou à la caméra dans les réglages du téléphone.", isError: true);
        }

        if (file is not null)
        {
            await UploadProfilePhotoAsync(file);
        }
    }

    private async Task UploadProfilePhotoAsync(FileResult file)
    {
        isUploadingPhoto = true;
        PhotoActionButton.IsEnabled = false;
        ShowPhotoStatus("Envoi de la photo…", isError: false);

        try
        {
            var photoBytes = await ReadPhotoAsync(file);
            if (photoBytes.Length == 0)
            {
                ShowPhotoStatus("La photo sélectionnée est vide.", isError: true);
                return;
            }

            var result = await apiClient.UploadProfilePhotoAsync(photoBytes, file.FileName, file.ContentType);
            if (!result.IsSuccess || result.Response is null)
            {
                ShowPhotoStatus(result.ErrorMessage ?? "La photo n'a pas pu être envoyée.", isError: true);
                return;
            }

            ShowLocalProfilePhoto(photoBytes);
            ShowPhotoStatus("Photo mise à jour.", isError: false);
        }
        catch (UnauthorizedAccessException)
        {
            ShowPhotoStatus("L'application ne peut pas lire cette photo.", isError: true);
        }
        catch (IOException)
        {
            ShowPhotoStatus("Cette photo ne peut pas être lue.", isError: true);
        }
        catch (Exception)
        {
            ShowPhotoStatus("La photo n'a pas pu être envoyée. Réessayez.", isError: true);
        }
        finally
        {
            isUploadingPhoto = false;
            PhotoActionButton.IsEnabled = true;
        }
    }

    private static async Task<byte[]> ReadPhotoAsync(FileResult file)
    {
        await using var stream = await file.OpenReadAsync();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private void ShowLocalProfilePhoto(byte[] bytes)
    {
        if (bytes.Length > 0)
        {
            ProfilePhotoImage.Source = ImageSource.FromStream(() => new MemoryStream(bytes, writable: false));
        }
    }

    private void ShowPhotoStatus(string message, bool isError)
    {
        PhotoStatusLabel.Text = message;
        PhotoStatusLabel.TextColor = Color.FromArgb(isError ? "#DC2626" : "#155EEF");
        PhotoStatusLabel.IsVisible = true;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FirstNameEntry.Text) || string.IsNullOrWhiteSpace(LastNameEntry.Text))
        {
            ShowError("Le prénom et le nom sont obligatoires.");
            return;
        }

        SaveButton.IsEnabled = false;
        var result = await apiClient.UpdateMeAsync(new UpdateClientProfileRequest(
            FirstNameEntry.Text.Trim(),
            LastNameEntry.Text.Trim(),
            EmailEntry.Text?.Trim()));
        SaveButton.IsEnabled = true;

        if (!result.IsSuccess)
        {
            ShowError(result.ErrorMessage ?? "Modification impossible.");
            return;
        }

        ErrorLabel.IsVisible = false;
        await DisplayAlert("Profil mis à jour", "Vos informations ont été enregistrées.", "OK");
    }

    private void ShowError(string text)
    {
        ErrorLabel.Text = text;
        ErrorLabel.IsVisible = true;
    }

    private async void OnBackClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
