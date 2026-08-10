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
    private Guid? pendingPaymentRequestId;
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
        if (!missionResult.IsSuccess || missionResult.Response is null)
        {
            ShowError(missionResult.ErrorMessage);
            return;
        }

        var mission = missionResult.Response;
        if (mission.CustomerConfirmedAt is not null
            && (mission.PaymentStatus.Equals("Authorized", StringComparison.OrdinalIgnoreCase)
                || mission.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase)))
        {
            ClearPendingPayment();
            await Shell.Current.GoToAsync($"{nameof(PaymentSuccessPage)}?missionId={missionId:D}");
            return;
        }

        var paymentWindowIsOpen = mission.Status.Equals("Accepted", StringComparison.OrdinalIgnoreCase)
            && mission.QuoteStatus.Equals("Submitted", StringComparison.OrdinalIgnoreCase)
            && mission.PaymentStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase)
            && mission.CustomerConfirmedAt is null
            && mission.CompanyQuotedAmount is > 0;
        if (!paymentWindowIsOpen)
        {
            PayButton.IsEnabled = false;
            ShowError("Le paiement sera disponible lorsque le prestataire aura accepté la mission.");
            return;
        }

        var paymentResult = await api.GetPaymentMethodsAsync();
        if (!paymentResult.IsSuccess || paymentResult.Response is null)
        {
            ShowError(paymentResult.ErrorMessage);
            return;
        }

        var previewResult = await api.GetMissionPaymentPreviewAsync(missionId);
        if (!previewResult.IsSuccess || previewResult.Response is null)
        {
            ShowError(previewResult.ErrorMessage);
            PayButton.IsEnabled = false;
            return;
        }

        currentMethodId = mission.CustomerPaymentMethodId;
        var preview = previewResult.Response;
        var amount = preview.TotalAmount;
        ServiceLabel.Text = string.Join(
            " · ",
            new[] { mission.ServiceName, mission.PrestationName, mission.OptionName }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        ServiceAmountLabel.Text = $"{mission.ServiceAmount:N0} {mission.Currency}";
        PartsRow.IsVisible = mission.PartsEstimateAmount is > 0;
        PartsAmountLabel.Text = $"{mission.PartsEstimateAmount.GetValueOrDefault():N0} {mission.Currency}";
        ServiceFeeLabel.Text = $"Frais de mise en relation Wélé ({mission.CustomerServiceFeeRateBasisPoints / 100m:0.##} %)";
        ServiceFeeAmountLabel.Text = $"{preview.CustomerServiceFeeAmount:N0} {preview.Currency}";
        PaymentProviderFeeLabel.Text = $"Frais Jeko Mobile Money ({preview.PaymentProviderFeeRateBasisPoints / 100m:0.##} %)";
        PaymentProviderFeeAmountLabel.Text = $"{preview.PaymentProviderFeeAmount:N0} {preview.Currency}";
        TotalLabel.Text = $"{amount:N0} {preview.Currency}";
        PayButton.Text = $"Payer {amount:N0} {preview.Currency}";

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
            return;
        }

        pendingPaymentRequestId = ReadPendingPayment();
        if (pendingPaymentRequestId.HasValue)
        {
            await RefreshPendingPaymentAsync(pollBriefly: true);
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

        if (pendingPaymentRequestId.HasValue)
        {
            await RefreshPendingPaymentAsync(pollBriefly: false, reopenRedirect: true);
            return;
        }

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

        var result = await api.StartMissionPaymentAsync(missionId, selected.Id);
        if (!result.IsSuccess)
        {
            ShowError(result.ErrorMessage);
            PayButton.IsEnabled = true;
            return;
        }

        var payment = result.Response!;
        pendingPaymentRequestId = payment.Id;
        Preferences.Default.Set(PendingPaymentKey(), payment.Id.ToString("D"));
        UpdatePaymentAmounts(payment);

        if (IsSuccess(payment.Status))
        {
            ClearPendingPayment();
            await Shell.Current.GoToAsync($"{nameof(PaymentSuccessPage)}?missionId={missionId:D}");
            return;
        }

        if (IsError(payment.Status))
        {
            ClearPendingPayment();
            ShowError(payment.Message ?? "Le paiement Jeko n'a pas abouti.");
            PayButton.IsEnabled = true;
            return;
        }

        await OpenJekoAsync(payment.RedirectUrl);
    }

    private async Task RefreshPendingPaymentAsync(bool pollBriefly, bool reopenRedirect = false)
    {
        if (!pendingPaymentRequestId.HasValue)
        {
            return;
        }

        ClientMissionPaymentResponse? payment = null;
        var attempts = pollBriefly ? 5 : 1;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var result = await api.GetMissionPaymentAsync(missionId, pendingPaymentRequestId.Value);
            if (!result.IsSuccess || result.Response is null)
            {
                if (result.StatusCode == 404)
                {
                    ClearPendingPayment();
                }

                ShowError(result.ErrorMessage);
                PayButton.IsEnabled = true;
                return;
            }

            payment = result.Response;
            UpdatePaymentAmounts(payment);
            if (!payment.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (attempt + 1 < attempts)
            {
                PayButton.Text = "Vérification du paiement...";
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }

        if (payment is null)
        {
            PayButton.IsEnabled = true;
            return;
        }

        if (IsSuccess(payment.Status))
        {
            ClearPendingPayment();
            await Shell.Current.GoToAsync($"{nameof(PaymentSuccessPage)}?missionId={missionId:D}");
            return;
        }

        if (IsError(payment.Status))
        {
            ClearPendingPayment();
            ShowError(payment.Message ?? "Le paiement Jeko n'a pas abouti.");
            PayButton.Text = $"Réessayer · {payment.TotalAmount:N0} {payment.Currency}";
            PayButton.IsEnabled = true;
            return;
        }

        PayButton.Text = "Vérifier le paiement";
        PayButton.IsEnabled = true;
        if (reopenRedirect)
        {
            await OpenJekoAsync(payment.RedirectUrl);
        }
    }

    private async Task OpenJekoAsync(string? redirectUrl)
    {
        if (!Uri.TryCreate(redirectUrl, UriKind.Absolute, out var uri))
        {
            ShowError("Jeko prépare le paiement. Touchez Vérifier dans quelques secondes.");
            PayButton.Text = "Vérifier le paiement";
            PayButton.IsEnabled = true;
            return;
        }

        var opened = await Launcher.Default.OpenAsync(uri);
        if (!opened)
        {
            ShowError("Impossible d'ouvrir la page de paiement Jeko.");
            PayButton.IsEnabled = true;
        }
    }

    private void UpdatePaymentAmounts(ClientMissionPaymentResponse payment)
    {
        PaymentProviderFeeAmountLabel.Text = $"{payment.PaymentProviderFeeAmount:N0} {payment.Currency}";
        TotalLabel.Text = $"{payment.TotalAmount:N0} {payment.Currency}";
    }

    private string PendingPaymentKey() => $"PendingJekoPayment:{missionId:N}";

    private Guid? ReadPendingPayment() =>
        Guid.TryParse(Preferences.Default.Get(PendingPaymentKey(), string.Empty), out var value) ? value : null;

    private void ClearPendingPayment()
    {
        Preferences.Default.Remove(PendingPaymentKey());
        pendingPaymentRequestId = null;
    }

    private static bool IsSuccess(string status) => status.Equals("Success", StringComparison.OrdinalIgnoreCase);
    private static bool IsError(string status) => status.Equals("Error", StringComparison.OrdinalIgnoreCase);

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
