using System.Collections.ObjectModel;
using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

[QueryProperty(nameof(MissionId), "missionId")]
[QueryProperty(nameof(AccountId), "accountId")]
public partial class AddPaymentMethodPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient;
    private readonly ObservableCollection<ProviderRow> providers = [];
    private bool initialized;
    private Guid? editingPaymentMethodId;

    public AddPaymentMethodPage()
    {
        InitializeComponent();
        apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
        ProvidersView.ItemsSource = providers;
    }

    public string? MissionId { get; set; }
    public string? AccountId { get; set; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (initialized)
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

        initialized = true;

        var isEditing = Guid.TryParse(AccountId, out var accountId);
        var availableProviders = isEditing
            ? result.Response.Where(provider => provider.Method == "MobileMoney")
            : result.Response;
        var rows = await Task.WhenAll(availableProviders.Select(async provider =>
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

        if (isEditing)
        {
            await PrepareEditModeAsync(accountId);
        }

        UpdateReferenceEditor();
    }

    private async Task PrepareEditModeAsync(Guid paymentMethodId)
    {
        var result = await apiClient.GetPaymentMethodsAsync();
        var target = result.Response?.FirstOrDefault(method => method.Id == paymentMethodId && method.Method == "MobileMoney");
        if (!result.IsSuccess || target is null || string.IsNullOrWhiteSpace(target.MaskedReference))
        {
            ShowError("Ce numéro Mobile Money n'a pas pu être chargé.");
            SaveButton.IsEnabled = false;
            return;
        }

        editingPaymentMethodId = target.Id;
        var selectedProviderIds = result.Response!
            .Where(method => method.Method == "MobileMoney" && method.MaskedReference == target.MaskedReference)
            .Select(method => method.PaymentProviderId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();
        foreach (var provider in providers)
        {
            provider.IsSelected = selectedProviderIds.Contains(provider.Provider.Id);
        }

        PageTitleLabel.Text = "Modifier Mobile Money";
        IntroTitleLabel.Text = "Réseaux associés";
        IntroDescriptionLabel.Text = "Ajoutez ou retirez les réseaux disponibles sur ce numéro.";
        ReferenceLabel.Text = "Numéro Mobile Money";
        ReferenceEntry.Text = target.MaskedReference;
        ReferenceEntry.IsReadOnly = true;
        ReferenceEntry.TextColor = Color.FromArgb("#667085");
        DefaultPanel.IsVisible = false;
        HelpLabel.Text = "Le numéro reste protégé et ne peut pas être modifié. Vous pouvez uniquement changer les réseaux associés.";
        SaveButton.Text = "Enregistrer les réseaux";
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
        if (editingPaymentMethodId.HasValue)
        {
            return;
        }

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
        if (selected.Count == 0 || (!editingPaymentMethodId.HasValue && string.IsNullOrWhiteSpace(reference)))
        {
            ErrorLabel.Text = editingPaymentMethodId.HasValue
                ? "Conservez au moins un réseau Mobile Money."
                : "Choisissez au moins un réseau et renseignez le numéro du compte.";
            ErrorLabel.IsVisible = true;
            return;
        }

        SaveButton.IsEnabled = false;
        var success = editingPaymentMethodId.HasValue
            ? await UpdateMobileMoneyAsync(selected)
            : selected[0].IsCard
                ? await SaveCardAsync(selected[0], reference!)
                : await SaveMobileMoneyAsync(selected, reference!);
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

    private async Task<bool> UpdateMobileMoneyAsync(IReadOnlyList<ProviderRow> selected)
    {
        if (!editingPaymentMethodId.HasValue)
        {
            return false;
        }

        var result = await apiClient.UpdateMobileMoneyAccountAsync(
            editingPaymentMethodId.Value,
            new UpdateClientMobileMoneyAccountRequest(selected.Select(provider => provider.Provider.Id).ToList()));
        if (!result.IsSuccess || result.Response is null)
        {
            ShowError(result.ErrorMessage ?? "Les réseaux Mobile Money n'ont pas pu être modifiés.");
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
