using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

public partial class ProfileInformationPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
    public ProfileInformationPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var result = await apiClient.GetMeAsync();
        if (result.IsSuccess && result.Response is not null)
        {
            FirstNameEntry.Text = result.Response.FirstName;
            LastNameEntry.Text = result.Response.LastName;
            PhoneEntry.Text = result.Response.PhoneNumber;
            EmailEntry.Text = result.Response.Email;
        }
        else ShowError(result.ErrorMessage ?? "Impossible de charger vos informations.");
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FirstNameEntry.Text) || string.IsNullOrWhiteSpace(LastNameEntry.Text))
        { ShowError("Le prénom et le nom sont obligatoires."); return; }
        SaveButton.IsEnabled = false;
        var result = await apiClient.UpdateMeAsync(new UpdateClientProfileRequest(FirstNameEntry.Text.Trim(), LastNameEntry.Text.Trim(), EmailEntry.Text?.Trim()));
        SaveButton.IsEnabled = true;
        if (!result.IsSuccess) { ShowError(result.ErrorMessage ?? "Modification impossible."); return; }
        ErrorLabel.IsVisible = false;
        await DisplayAlert("Profil mis à jour", "Vos informations ont été enregistrées.", "OK");
    }
    private void ShowError(string text) { ErrorLabel.Text = text; ErrorLabel.IsVisible = true; }
    private async void OnBackClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
