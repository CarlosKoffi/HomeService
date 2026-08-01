using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

[QueryProperty(nameof(MissionId), "missionId")]
public partial class AddPaymentMethodPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient;
    private IReadOnlyList<PaymentProviderResponse> providers = [];

    public AddPaymentMethodPage()
    {
        InitializeComponent();
        apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
    }

    public string? MissionId { get; set; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (providers.Count > 0) return;
        var result = await apiClient.GetPaymentProvidersAsync();
        if (!result.IsSuccess || result.Response is null)
        {
            CatalogErrorLabel.IsVisible = true;
            SaveButton.IsEnabled = false;
            return;
        }
        providers = result.Response;
        ProviderPicker.ItemsSource = providers.ToList();
        ProviderPicker.SelectedIndex = providers.Count > 0 ? 0 : -1;
    }

    private void OnProviderChanged(object sender, EventArgs e)
    {
        var isCard = ProviderPicker.SelectedItem is PaymentProviderResponse { Method: "Card" };
        ReferenceLabel.Text = isCard ? "Quatre derniers chiffres" : "Numero Mobile Money";
        ReferenceEntry.Placeholder = isCard ? "Ex. 4242" : "07 00 00 00 00";
        ReferenceEntry.Keyboard = isCard ? Keyboard.Numeric : Keyboard.Telephone;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        var reference = ReferenceEntry.Text?.Trim();
        if (ProviderPicker.SelectedItem is not PaymentProviderResponse provider || string.IsNullOrWhiteSpace(reference))
        {
            ErrorLabel.Text = "Choisissez un operateur et renseignez le numero du compte.";
            ErrorLabel.IsVisible = true;
            return;
        }

        SaveButton.IsEnabled = false;
        var result = await apiClient.CreatePaymentMethodAsync(new UpsertClientPaymentMethodRequest(
            provider.Method,
            provider.Name,
            Mask(reference),
            DefaultCheckBox.IsChecked,
            provider.Id));
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
