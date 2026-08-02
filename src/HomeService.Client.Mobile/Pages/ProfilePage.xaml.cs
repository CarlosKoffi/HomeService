using HomeService.Client.Mobile.Services;

namespace HomeService.Client.Mobile.Pages;

public partial class ProfilePage : ContentPage
{
    private readonly ClientMobileApiClient apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
    private readonly ClientSessionStore sessionStore = MobileServiceLocator.GetRequiredService<ClientSessionStore>();
    private bool isUploadingPhoto;
    private bool isLoading;

    public ProfilePage() => InitializeComponent();

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
            await LoadAsync();
        }
        catch (Exception)
        {
            // A profile refresh must never terminate the mobile application.
            ShowPhotoStatus("Le profil n'a pas pu etre actualise. Reessayez dans un instant.", isError: true);
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task LoadAsync()
    {
        LoggedOutCard.IsVisible = !sessionStore.HasSession();
        LoggedInSection.IsVisible = sessionStore.HasSession();
        if (!sessionStore.HasSession())
        {
            return;
        }

        if (sessionStore.IsPreviewMode())
        {
            SetIdentity("Carlos", "Konan", "+225 07 00 00 00 00");
            return;
        }

        var result = await apiClient.GetMeAsync();
        if (!result.IsSuccess || result.Response is null)
        {
            ShowPhotoStatus(result.ErrorMessage ?? "Profil indisponible pour le moment.", isError: true);
            return;
        }

        SetIdentity(result.Response.FirstName, result.Response.LastName, result.Response.PhoneNumber);
        await LoadProfilePhotoAsync(result.Response.ProfilePhotoUrl);
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
            // Keep the local placeholder when the remote photo is unavailable.
        }
    }

    private async void OnProfilePhotoClicked(object sender, TappedEventArgs e)
    {
        if (isUploadingPhoto || sessionStore.IsPreviewMode())
        {
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
            ShowPhotoStatus("La camera ou la galerie n'est pas disponible sur cet appareil.", isError: true);
        }
        catch (PermissionException)
        {
            ShowPhotoStatus("Autorisez l'acces aux photos ou a la camera dans les reglages du telephone.", isError: true);
        }

        if (file is null)
        {
            return;
        }

        await UploadProfilePhotoAsync(file);
    }

    private async Task UploadProfilePhotoAsync(FileResult file)
    {
        isUploadingPhoto = true;
        ShowPhotoStatus("Envoi de la photo...", isError: false);

        try
        {
            var photoBytes = await ReadPhotoAsync(file);
            if (photoBytes.Length == 0)
            {
                ShowPhotoStatus("La photo selectionnee est vide. Choisissez une autre photo.", isError: true);
                return;
            }

            ShowLocalProfilePhoto(photoBytes);
            var result = await apiClient.UploadProfilePhotoAsync(photoBytes, file.FileName);
            if (!result.IsSuccess || result.Response is null)
            {
                ShowPhotoStatus(result.ErrorMessage ?? "La photo n'a pas pu etre envoyee.", isError: true);
                return;
            }

            var remoteImage = await apiClient.DownloadProfilePhotoAsync(result.Response.ProfilePhotoUrl);
            if (remoteImage is not null)
            {
                ProfilePhotoImage.Source = remoteImage;
            }
            ShowPhotoStatus("Photo mise a jour.", isError: false);
        }
        catch (UnauthorizedAccessException)
        {
            ShowPhotoStatus("L'application ne peut pas lire cette photo. Verifiez les autorisations.", isError: true);
        }
        catch (IOException)
        {
            ShowPhotoStatus("La photo ne peut pas etre lue. Choisissez-la de nouveau.", isError: true);
        }
        catch (Exception)
        {
            ShowPhotoStatus("La photo n'a pas pu etre envoyee. Reessayez.", isError: true);
        }
        finally
        {
            isUploadingPhoto = false;
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
        PhotoStatusLabel.TextColor = Color.FromArgb(isError ? "#C62828" : "#1769E8");
        PhotoStatusLabel.IsVisible = true;
    }

    private void SetIdentity(string firstName, string lastName, string phone)
    {
        NameLabel.Text = $"{firstName} {lastName}";
        PhoneLabel.Text = phone;
    }

    private async void OnLoginClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(LoginPage));
    private async void OnRegisterClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(RegisterPage));
    private async void OnInformationClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(ProfileInformationPage));
    private async void OnAddressesClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(AddressesPage));
    private async void OnPaymentMethodsClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(PaymentMethodsPage));
    private async void OnRequestsClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("//requests");
    private async void OnReviewsClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(ReviewsPage));
    private async void OnNotificationsClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(ClientNotificationsPage));
    private async void OnSettingsClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(ClientSettingsPage));
}
