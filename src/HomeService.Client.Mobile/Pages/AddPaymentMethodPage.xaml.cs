using System.Collections.ObjectModel;
using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

[QueryProperty(nameof(MissionId), "missionId")]
public partial class AddPaymentMethodPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient;
    private readonly ObservableCollection<ProviderRow> providers = [];

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
        if (providers.Count > 0)
        {
            return;
        }

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
        foreach (var row in rows)
        {
            providers.Add(row);
        }

        UpdateReferenceEditor();
    }

    private void OnProviderTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not ProviderRow selected)
        {
            return;
        }

        if (selected.IsCard)
        {
            foreach (var provider in providers)
            {
                provider.IsSelected = ReferenceEquals(provider, selected);
            }
        }
        else
        {
            foreach (var provider in providers.Where(provider => provider.IsCard))
            {
                provider.IsSelected = false;
            }

            selected.IsSelected = !selected.IsSelected;
        }

        UpdateReferenceEditor();
    }

    private void UpdateReferenceEditor()
    {
        var isCard = providers.Any(provider => provider.IsSelected && provider.IsCard);
        ReferenceLabel.Text = isCard ? "Quatre derniers chiffres" : "Numéro Mobile Money";
        ReferenceEntry.Placeholder = isCard ? "Ex. 4242" : "07 00 00 00 00";
        ReferenceEntry.Keyboard = isCard ? Keyboard.Numeric : Keyboard.Telephone;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        var reference = ReferenceEntry.Text?.Trim();
        var selected = providers.Where(provider => provider.IsSelected).ToList();
        if (selected.Count == 0 || string.IsNullOrWhiteSpace(reference))
        {
            ErrorLabel.Text = "Choisissez au moins un réseau et renseignez le numéro du compte.";
            ErrorLabel.IsVisible = true;
            return;
        }

        SaveButton.IsEnabled = false;
        var success = selected[0].IsCard
            ? await SaveCardAsync(selected[0], reference)
            : await SaveMobileMoneyAsync(selected, reference);
        SaveButton.IsEnabled = true;
        if (!success)
        {
            return;
        }

        ReferenceEntry.Text = string.Empty;
        await Shell.Current.GoToAsync("..");
    }

    private async Task<bool> SaveCardAsync(ProviderRow selected, string reference)
    {
        var digits = new string(reference.Where(char.IsDigit).ToArray());
        if (digits.Length != 4)
        {
            ShowError("Saisissez les quatre derniers chiffres de la carte.");
            return false;
        }

        var provider = selected.Provider;
        var result = await apiClient.CreatePaymentMethodAsync(new UpsertClientPaymentMethodRequest(
            provider.Method,
            provider.Name,
            $"**** {digits}",
            DefaultCheckBox.IsChecked,
            provider.Id));
        if (!result.IsSuccess || result.Response is null)
        {
            ShowError(result.ErrorMessage ?? "La carte n'a pas pu être enregistrée.");
            return false;
        }

        return true;
    }

    private async Task<bool> SaveMobileMoneyAsync(IReadOnlyList<ProviderRow> selected, string phoneNumber)
    {
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (digits.Length is < 8 or > 15)
        {
            ShowError("Saisissez un numéro Mobile Money valide.");
            return false;
        }

        var result = await apiClient.CreateMobileMoneyAccountAsync(new CreateClientMobileMoneyAccountRequest(
            phoneNumber,
            selected.Select(provider => provider.Provider.Id).ToList(),
            DefaultCheckBox.IsChecked));
        if (!result.IsSuccess || result.Response is null)
        {
            ShowError(result.ErrorMessage ?? "Le numéro Mobile Money n'a pas pu être enregistré.");
            return false;
        }

        return true;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
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
        public string Description => Provider.Method == "Card"
            ? "Carte bancaire"
            : "Disponible avec ce numéro Mobile Money";
        public ImageSource? LogoSource { get; }
        public bool ShowFallback => LogoSource is null;
        public string Fallback => IsCard ? "CB" : "MM";
        public bool IsCard => Provider.Method == "Card";
        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (isSelected == value)
                {
                    return;
                }

                isSelected = value;
                OnPropertyChanged();
            }
        }

        public static ProviderRow From(PaymentProviderResponse provider, ImageSource? logo) => new(provider, logo);
    }
}
