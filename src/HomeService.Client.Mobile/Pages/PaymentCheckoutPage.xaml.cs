using System.Collections.ObjectModel;
using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

[QueryProperty(nameof(MissionId), "missionId")]
public partial class PaymentCheckoutPage : ContentPage
{
    private readonly ClientMobileApiClient api = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
    private readonly ObservableCollection<PaymentAccountRow> accounts = [];
    private Guid missionId;
    private Guid? currentMethodId;
    private PaymentNetworkRow? selected;

    public PaymentCheckoutPage()
    {
        InitializeComponent();
        MethodsView.ItemsSource = accounts;
    }

    public string? MissionId { get; set; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!Guid.TryParse(MissionId, out missionId))
        {
            ShowError("Mission introuvable.");
            return;
        }

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        ErrorLabel.IsVisible = false;
        accounts.Clear();
        selected = null;

        var missionResult = await api.GetMissionAsync(missionId);
        var paymentResult = await api.GetPaymentMethodsAsync();
        if (!missionResult.IsSuccess || missionResult.Response is null)
        {
            ShowError(missionResult.ErrorMessage);
            return;
        }

        if (!paymentResult.IsSuccess || paymentResult.Response is null)
        {
            ShowError(paymentResult.ErrorMessage);
            return;
        }

        var mission = missionResult.Response;
        currentMethodId = mission.CustomerPaymentMethodId;
        var amount = mission.Actions.AmountToPayNow
            ?? mission.CompanyQuotedAmount
            ?? mission.FinalTotalAmount
            ?? mission.EstimatedTotalAmount
            ?? 0;
        ServiceLabel.Text = string.Join(
            " · ",
            new[] { mission.ServiceName, mission.PrestationName, mission.OptionName }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        ServiceAmountLabel.Text = $"{Math.Max(0, amount - mission.PartsEstimateAmount.GetValueOrDefault()):N0} {mission.Currency}";
        PartsRow.IsVisible = mission.PartsEstimateAmount is > 0;
        PartsAmountLabel.Text = $"{mission.PartsEstimateAmount.GetValueOrDefault():N0} {mission.Currency}";
        TotalLabel.Text = $"{amount:N0} {mission.Currency}";
        PayButton.Text = $"Payer {amount:N0} {mission.Currency}";

        var decoratedMethods = await Task.WhenAll(paymentResult.Response.Select(async method =>
            new DecoratedPaymentMethod(
                method,
                await PaymentProviderLogoResolver.ResolveAsync(
                    api,
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

        Select(accounts
                   .SelectMany(account => account.Networks)
                   .FirstOrDefault(network => network.Id == currentMethodId)
               ?? accounts.SelectMany(account => account.Networks).FirstOrDefault(network => network.IsDefault)
               ?? accounts.SelectMany(account => account.Networks).FirstOrDefault());

        if (selected is null)
        {
            await Shell.Current.GoToAsync($"{nameof(AddPaymentMethodPage)}?missionId={missionId:D}");
        }
    }

    private void OnAccountTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is PaymentAccountRow account)
        {
            Select(account.Networks.FirstOrDefault(network => network.IsDefault)
                   ?? account.Networks.FirstOrDefault());
        }
    }

    private void OnNetworkTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is PaymentNetworkRow network)
        {
            Select(network);
        }
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

        selected = row;
        PayButton.IsEnabled = row is not null;
    }

    private async void OnPayClicked(object sender, EventArgs e)
    {
        if (selected is null)
        {
            return;
        }

        PayButton.IsEnabled = false;
        ErrorLabel.IsVisible = false;

        if (currentMethodId != selected.Id)
        {
            var selectionResult = await api.SelectMissionPaymentMethodAsync(missionId, selected.Id);
            if (!selectionResult.IsSuccess)
            {
                ShowError(selectionResult.ErrorMessage);
                PayButton.IsEnabled = true;
                return;
            }
        }

        var result = await api.ConfirmMissionAsync(missionId, $"MOBILE-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}");
        if (!result.IsSuccess)
        {
            ShowError(result.ErrorMessage);
            PayButton.IsEnabled = true;
            return;
        }

        await Shell.Current.GoToAsync($"{nameof(PaymentSuccessPage)}?missionId={missionId:D}");
    }

    private async void OnBackClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    private void ShowError(string? message)
    {
        ErrorLabel.Text = message ?? "Action impossible pour le moment.";
        ErrorLabel.IsVisible = true;
    }

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
        public string AccountIconSource => IsMobileMoney ? "profile_payment.svg" : "payment_bank_card.png";
        public string NetworkCaption => IsMobileMoney ? "Réseaux" : "Type";
        public ObservableCollection<PaymentNetworkRow> Networks { get; }
        public bool IsDefault => Networks.Any(network => network.IsDefault);

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
            return new PaymentAccountRow("Mobile Money", rows[0].Reference, true, rows);
        }

        public static PaymentAccountRow Card(DecoratedPaymentMethod method) =>
            new(
                method.Method.PaymentProviderName ?? method.Method.Label,
                method.Method.MaskedReference ?? "Carte sécurisée",
                false,
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
