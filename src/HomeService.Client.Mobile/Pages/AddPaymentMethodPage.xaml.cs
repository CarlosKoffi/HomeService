using System.Collections.ObjectModel;
using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

[QueryProperty(nameof(MissionId), "missionId")]
public partial class AddPaymentMethodPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient;
    private readonly ObservableCollection<ProviderRow> providers = [];
    private ProviderRow? selectedProvider;

    public AddPaymentMethodPage()
    {
        InitializeComponent();
        apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
        ProvidersView.ItemsSource = providers;
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

        var rows = await Task.WhenAll(result.Response.Select(async provider =>
            ProviderRow.From(
                provider,
                await PaymentProviderLogoResolver.ResolveAsync(
                    apiClient,
                    provider.Code,
                    provider.Name,
                    provider.Method,
                    provider.LogoUrl))));
        foreach (var row in rows) providers.Add(row);
        Select(providers.FirstOrDefault());
    }

    private void OnProviderTapped(object sender, TappedEventArgs e) => Select(e.Parameter as ProviderRow);

    private void Select(ProviderRow? row)
    {
        foreach (var item in providers) item.IsSelected = ReferenceEquals(item, row);
        selectedProvider = row;
        var isCard = row?.Provider.Method == "Card";
        ReferenceLabel.Text = isCard ? "Quatre derniers chiffres" : "Numero Mobile Money";
        ReferenceEntry.Placeholder = isCard ? "Ex. 4242" : "07 00 00 00 00";
        ReferenceEntry.Keyboard = isCard ? Keyboard.Numeric : Keyboard.Telephone;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        var reference = ReferenceEntry.Text?.Trim();
        if (selectedProvider is not { } selected || string.IsNullOrWhiteSpace(reference))
        {
            ErrorLabel.Text = "Choisissez un operateur et renseignez le numero du compte.";
            ErrorLabel.IsVisible = true;
            return;
        }

        var provider = selected.Provider;
        SaveButton.IsEnabled = false;
        var result = await apiClient.CreatePaymentMethodAsync(new UpsertClientPaymentMethodRequest(provider.Method, provider.Name, Mask(reference), DefaultCheckBox.IsChecked, provider.Id));
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
        return $"**** {suffix}";
    }

    private async void OnBackClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    private sealed class ProviderRow : BindableObject
    {
        private bool isSelected;

        private ProviderRow(PaymentProviderResponse provider, ImageSource? logo)
        {
            Provider = provider;
            LogoSource = logo;
        }

        public PaymentProviderResponse Provider { get; }
        public string Name => Provider.Name;
        public string Description => Provider.Description ?? (Provider.Method == "Card" ? "Carte bancaire" : "Compte Mobile Money");
        public ImageSource? LogoSource { get; }
        public bool ShowFallback => false;
        public string Fallback => Provider.Method == "Card" ? "CB" : "MM";
        public bool IsSelected { get => isSelected; set { if (isSelected == value) return; isSelected = value; OnPropertyChanged(); } }
        public static ProviderRow From(PaymentProviderResponse provider, ImageSource? logo) => new(provider, logo);
    }
}
