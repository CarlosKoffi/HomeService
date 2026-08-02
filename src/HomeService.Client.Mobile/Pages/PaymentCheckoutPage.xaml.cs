using System.Collections.ObjectModel;
using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

[QueryProperty(nameof(MissionId), "missionId")]
public partial class PaymentCheckoutPage : ContentPage
{
    private readonly ClientMobileApiClient api = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
    private readonly ObservableCollection<PaymentRow> methods = [];
    private Guid missionId;
    private Guid? currentMethodId;
    private PaymentRow? selected;

    public PaymentCheckoutPage()
    {
        InitializeComponent();
        MethodsView.ItemsSource = methods;
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
        methods.Clear();
        var missionResult = await api.GetMissionAsync(missionId);
        var paymentResult = await api.GetPaymentMethodsAsync();
        if (!missionResult.IsSuccess || missionResult.Response is null) { ShowError(missionResult.ErrorMessage); return; }
        if (!paymentResult.IsSuccess || paymentResult.Response is null) { ShowError(paymentResult.ErrorMessage); return; }

        var mission = missionResult.Response;
        currentMethodId = mission.CustomerPaymentMethodId;
        var amount = mission.Actions.AmountToPayNow ?? mission.CompanyQuotedAmount ?? mission.FinalTotalAmount ?? mission.EstimatedTotalAmount ?? 0;
        ServiceLabel.Text = string.Join(" · ", new[] { mission.ServiceName, mission.PrestationName, mission.OptionName }.Where(x => !string.IsNullOrWhiteSpace(x)));
        ServiceAmountLabel.Text = $"{Math.Max(0, amount - mission.PartsEstimateAmount.GetValueOrDefault()):N0} {mission.Currency}";
        PartsRow.IsVisible = mission.PartsEstimateAmount is > 0;
        PartsAmountLabel.Text = $"{mission.PartsEstimateAmount.GetValueOrDefault():N0} {mission.Currency}";
        TotalLabel.Text = $"{amount:N0} {mission.Currency}";
        PayButton.Text = $"Payer {amount:N0} {mission.Currency}";

        foreach (var payment in paymentResult.Response)
        {
            var logo = await PaymentProviderLogoResolver.ResolveAsync(api, null, payment.PaymentProviderName ?? payment.Label, payment.Method, payment.PaymentProviderLogoUrl);
            methods.Add(new PaymentRow(payment, logo));
        }

        Select(methods.FirstOrDefault(x => x.Id == currentMethodId)
            ?? methods.FirstOrDefault(x => x.IsDefault)
            ?? methods.FirstOrDefault());

        if (selected is null)
        {
            await Shell.Current.GoToAsync($"{nameof(AddPaymentMethodPage)}?missionId={missionId:D}");
        }
    }

    private void OnMethodTapped(object sender, TappedEventArgs e) => Select(e.Parameter as PaymentRow);

    private void Select(PaymentRow? row)
    {
        foreach (var item in methods) item.IsSelected = ReferenceEquals(item, row);
        selected = row;
        PayButton.IsEnabled = row is not null;
    }

    private async void OnPayClicked(object sender, EventArgs e)
    {
        if (selected is null) return;
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
    private void ShowError(string? message) { ErrorLabel.Text = message ?? "Action impossible pour le moment."; ErrorLabel.IsVisible = true; }

    private sealed class PaymentRow(ClientPaymentMethodResponse value, ImageSource? logo) : BindableObject
    {
        private bool isSelected;
        public Guid Id => value.Id;
        public string Label => value.PaymentProviderName ?? value.Label;
        public string Reference => value.MaskedReference ?? "Compte sécurisé";
        public string Fallback => value.Method == "Card" ? "CB" : "MM";
        public ImageSource? LogoSource => logo;
        public bool ShowFallback => logo is null;
        public bool IsDefault => value.IsDefault;
        public bool IsSelected { get => isSelected; set { if (isSelected == value) return; isSelected = value; OnPropertyChanged(); } }
    }
}
