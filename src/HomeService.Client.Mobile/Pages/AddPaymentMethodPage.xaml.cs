using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

[QueryProperty(nameof(MissionId), "missionId")]
public partial class AddPaymentMethodPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient;

    public AddPaymentMethodPage()
    {
        InitializeComponent();
        apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
        MethodPicker.SelectedIndex = 0;
    }

    public string? MissionId { get; set; }

    private void OnMethodChanged(object sender, EventArgs e)
    {
        var isCard = MethodPicker.SelectedIndex == 1;
        ReferenceLabel.Text = isCard ? "Quatre derniers chiffres" : "Numero Mobile Money";
        ReferenceEntry.Placeholder = isCard ? "Ex. 4242" : "07 00 00 00 00";
        ReferenceEntry.Keyboard = isCard ? Keyboard.Numeric : Keyboard.Telephone;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        var label = LabelEntry.Text?.Trim();
        var reference = ReferenceEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(reference))
        {
            ErrorLabel.Text = "Renseignez un nom et la reference du compte.";
            ErrorLabel.IsVisible = true;
            return;
        }

        SaveButton.IsEnabled = false;
        var result = await apiClient.CreatePaymentMethodAsync(new UpsertClientPaymentMethodRequest(
            MethodPicker.SelectedIndex == 1 ? "Card" : "MobileMoney",
            label,
            Mask(reference),
            DefaultCheckBox.IsChecked));
        SaveButton.IsEnabled = true;

        if (!result.IsSuccess || result.Response is null)
        {
            ErrorLabel.Text = result.ErrorMessage ?? "Le moyen de paiement n'a pas pu etre enregistre.";
            ErrorLabel.IsVisible = true;
            return;
        }

        if (Guid.TryParse(MissionId, out var missionId))
        {
            var selection = await apiClient.SelectMissionPaymentMethodAsync(missionId, result.Response.Id);
            if (!selection.IsSuccess)
            {
                ErrorLabel.Text = selection.ErrorMessage ?? "Le moyen est enregistre mais n'a pas pu etre rattache a la demande.";
                ErrorLabel.IsVisible = true;
                return;
            }

            await Shell.Current.GoToAsync($"../{nameof(MissionDetailPage)}?missionId={missionId:D}");
            return;
        }

        await Shell.Current.GoToAsync("..");
    }

    private static string Mask(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        var suffix = digits.Length <= 4 ? digits : digits[^4..];
        return $"•••• {suffix}";
    }

    private async void OnBackClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
