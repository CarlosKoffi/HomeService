using System.Collections.ObjectModel;
using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

[QueryProperty(nameof(MissionId), "missionId")]
public partial class PaymentMethodsPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient;
    private readonly ObservableCollection<PaymentAccountRow> accounts = [];
    private PaymentNetworkRow? selectedMethod;

    public PaymentMethodsPage()
    {
        InitializeComponent();
        apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
        MethodsView.ItemsSource = accounts;
    }

    public string? MissionId { get; set; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        ErrorLabel.IsVisible = false;
        accounts.Clear();
        selectedMethod = null;
        ContinueButton.IsEnabled = false;

        var result = await apiClient.GetPaymentMethodsAsync();
        if (!result.IsSuccess || result.Response is null)
        {
            ErrorLabel.Text = result.ErrorMessage ?? "Impossible de charger vos moyens de paiement.";
            ErrorLabel.IsVisible = true;
            return;
        }

        var decoratedMethods = await Task.WhenAll(result.Response.Select(async method =>
            new DecoratedPaymentMethod(
                method,
                await PaymentProviderLogoResolver.ResolveAsync(
                    apiClient,
                    null,
                    method.PaymentProviderName ?? method.Label,
                    method.Method,
                    method.PaymentProviderLogoUrl))));

        foreach (var mobileAccount in decoratedMethods
                     .Where(item => item.Method.Method == "MobileMoney")
                     .GroupBy(item => item.Method.MaskedReference ?? item.Method.Id.ToString("D"))
                     .OrderByDescending(group => group.Any(item => item.Method.IsDefault)))
        {
            accounts.Add(PaymentAccountRow.MobileMoney(mobileAccount));
        }

        foreach (var card in decoratedMethods
                     .Where(item => item.Method.Method != "MobileMoney")
                     .OrderByDescending(item => item.Method.IsDefault))
        {
            accounts.Add(PaymentAccountRow.Card(card));
        }

        EmptyState.IsVisible = accounts.Count == 0;
        if (accounts.Count == 0 && Guid.TryParse(MissionId, out _))
        {
            await GoToAddAsync();
            return;
        }

        var defaultMethod = accounts
            .SelectMany(account => account.Networks)
            .FirstOrDefault(item => item.IsDefault)
            ?? accounts.SelectMany(account => account.Networks).FirstOrDefault();
        Select(defaultMethod);
    }

    private void OnAccountTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not PaymentAccountRow account)
        {
            return;
        }

        Select(account.Networks.FirstOrDefault(network => network.IsDefault) ?? account.Networks.FirstOrDefault());
    }

    private void OnNetworkTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is PaymentNetworkRow network)
        {
            Select(network);
        }
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: PaymentAccountRow account } || !account.IsMobileMoney)
        {
            return;
        }

        await Shell.Current.GoToAsync(
            $"{nameof(AddPaymentMethodPage)}?missionId={Uri.EscapeDataString(MissionId ?? string.Empty)}&accountId={account.PrimaryId:D}");
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: PaymentAccountRow account })
        {
            return;
        }

        var confirmed = await DisplayAlert(
            "Supprimer ce moyen ?",
            $"{account.Reference} et ses réseaux ne seront plus proposés pour vos paiements.",
            "Supprimer",
            "Annuler");
        if (!confirmed)
        {
            return;
        }

        ErrorLabel.IsVisible = false;
        foreach (var methodId in account.MethodIds)
        {
            var result = await apiClient.DeletePaymentMethodAsync(methodId);
            if (!result.IsSuccess)
            {
                ErrorLabel.Text = result.ErrorMessage ?? "Ce moyen de paiement n'a pas pu être supprimé.";
                ErrorLabel.IsVisible = true;
                return;
            }
        }

        await LoadAsync();
    }

    private void Select(PaymentNetworkRow? row)
    {
        foreach (var account in accounts)
        {
            foreach (var network in account.Networks)
            {
                network.IsSelected = ReferenceEquals(network, row);
            }

            account.IsSelected = account.Networks.Any(network => network.IsSelected);
        }

        selectedMethod = row;
        ContinueButton.IsEnabled = row is not null;
    }

    private async void OnContinueClicked(object sender, EventArgs e)
    {
        if (selectedMethod is not { } selected)
        {
            return;
        }

        if (Guid.TryParse(MissionId, out var missionId))
        {
            ContinueButton.IsEnabled = false;
            var result = await apiClient.SelectMissionPaymentMethodAsync(missionId, selected.Id);
            ContinueButton.IsEnabled = true;
            if (!result.IsSuccess)
            {
                ErrorLabel.Text = result.ErrorMessage ?? "Ce moyen de paiement n'a pas pu être sélectionné.";
                ErrorLabel.IsVisible = true;
                return;
            }

            await Shell.Current.GoToAsync($"{nameof(MissionDetailPage)}?missionId={missionId:D}");
            return;
        }

        await Shell.Current.GoToAsync("..");
    }

    private async void OnAddClicked(object sender, EventArgs e) => await GoToAddAsync();

    private async Task GoToAddAsync() =>
        await Shell.Current.GoToAsync(
            $"{nameof(AddPaymentMethodPage)}?missionId={Uri.EscapeDataString(MissionId ?? string.Empty)}");

    private async void OnBackClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    private sealed record DecoratedPaymentMethod(ClientPaymentMethodResponse Method, ImageSource? Logo);

    private sealed class PaymentAccountRow : BindableObject
    {
        private bool isSelected;

        private PaymentAccountRow(
            string label,
            string reference,
            bool isMobileMoney,
            IEnumerable<PaymentNetworkRow> networks)
        {
            Label = label;
            Reference = reference;
            IsMobileMoney = isMobileMoney;
            Networks = new ObservableCollection<PaymentNetworkRow>(networks);
        }

        public string Label { get; }
        public string Reference { get; }
        public bool IsMobileMoney { get; }
        public string IconText => IsMobileMoney ? "MOMO" : "CARTE";
        public string AccountIconSource => IsMobileMoney ? "profile_payment.svg" : "payment_bank_card.png";
        public string NetworkCaption => IsMobileMoney ? "Réseaux" : "Type";
        public ObservableCollection<PaymentNetworkRow> Networks { get; }
        public bool IsDefault => Networks.Any(network => network.IsDefault);
        public Guid PrimaryId => Networks.FirstOrDefault(network => network.IsDefault)?.Id ?? Networks[0].Id;
        public IReadOnlyList<Guid> MethodIds => Networks.Select(network => network.Id).ToList();
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

        public static PaymentAccountRow MobileMoney(IEnumerable<DecoratedPaymentMethod> methods)
        {
            var rows = methods
                .OrderBy(item => item.Method.PaymentProviderName ?? item.Method.Label)
                .Select(PaymentNetworkRow.From)
                .ToList();
            return new PaymentAccountRow(
                "Mobile Money",
                rows[0].Reference,
                isMobileMoney: true,
                rows);
        }

        public static PaymentAccountRow Card(DecoratedPaymentMethod method) =>
            new(
                method.Method.PaymentProviderName ?? method.Method.Label,
                method.Method.MaskedReference ?? "Carte sécurisée",
                isMobileMoney: false,
                [PaymentNetworkRow.From(method)]);
    }

    private sealed class PaymentNetworkRow : BindableObject
    {
        private bool isSelected;

        private PaymentNetworkRow(
            Guid id,
            string name,
            string reference,
            ImageSource? logoSource,
            bool isDefault,
            bool isCard)
        {
            Id = id;
            Name = name;
            Reference = reference;
            LogoSource = logoSource;
            IsDefault = isDefault;
            IsCard = isCard;
        }

        public Guid Id { get; }
        public string Name { get; }
        public string Reference { get; }
        public ImageSource? LogoSource { get; }
        public bool ShowFallback => LogoSource is null;
        public bool IsDefault { get; }
        public bool IsCard { get; }
        public string Fallback => IsCard ? "CB" : Initials(Name);
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

        public static PaymentNetworkRow From(DecoratedPaymentMethod item) =>
            new(
                item.Method.Id,
                item.Method.PaymentProviderName ?? item.Method.Label,
                item.Method.MaskedReference ?? "Compte sécurisé",
                item.Logo,
                item.Method.IsDefault,
                item.Method.Method == "Card");

        private static string Initials(string value)
        {
            var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(words.Take(2).Select(word => char.ToUpperInvariant(word[0])));
        }
    }
}
