using HomeService.Client.Mobile.Services;

namespace HomeService.Client.Mobile.Pages;

public partial class ProfilePage : ContentPage
{
    private readonly ClientMobileApiClient apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
    private readonly ClientSessionStore sessionStore = MobileServiceLocator.GetRequiredService<ClientSessionStore>();
    private bool isUploadingPhoto;

    public ProfilePage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
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
        ProfilePhotoImage.Source = "nav_profile.svg";
        if (string.IsNullOrWhiteSpace(photoUrl))
        {
            return;
        }

        var image = await apiClient.DownloadProfilePhotoAsync(photoUrl);
        if (image is not null)
        {
            ProfilePhotoImage.Source = image;
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
            var result = await apiClient.UploadProfilePhotoAsync(file);
            if (!result.IsSuccess || result.Response is null)
            {
                ShowPhotoStatus(result.ErrorMessage ?? "La photo n'a pas pu etre envoyee.", isError: true);
                return;
            }

            await LoadProfilePhotoAsync(result.Response.ProfilePhotoUrl);
            ShowPhotoStatus("Photo mise a jour.", isError: false);
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
