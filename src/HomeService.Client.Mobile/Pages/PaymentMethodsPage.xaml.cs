using System.Collections.ObjectModel;
using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

[QueryProperty(nameof(MissionId), "missionId")]
public partial class PaymentMethodsPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient;
    private readonly ObservableCollection<PaymentMethodRow> methods = [];
    private PaymentMethodRow? selectedMethod;

    public PaymentMethodsPage()
    {
        InitializeComponent();
        apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
        MethodsView.ItemsSource = methods;
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
        methods.Clear();
        var result = await apiClient.GetPaymentMethodsAsync();
        if (!result.IsSuccess || result.Response is null)
        {
            ErrorLabel.Text = result.ErrorMessage ?? "Impossible de charger vos moyens de paiement.";
            ErrorLabel.IsVisible = true;
            return;
        }

        var rows = await Task.WhenAll(result.Response.Select(async method =>
            PaymentMethodRow.From(
                method,
                await PaymentProviderLogoResolver.ResolveAsync(
                    apiClient,
                    null,
                    method.PaymentProviderName ?? method.Label,
                    method.Method,
                    method.PaymentProviderLogoUrl))));
        foreach (var row in rows) methods.Add(row);

        EmptyState.IsVisible = methods.Count == 0;
        if (methods.Count == 0 && Guid.TryParse(MissionId, out _))
        {
            await GoToAddAsync();
            return;
        }

        Select(methods.FirstOrDefault(item => item.IsDefault) ?? methods.FirstOrDefault());
    }

    private void OnMethodTapped(object sender, TappedEventArgs e) => Select(e.Parameter as PaymentMethodRow);

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: PaymentMethodRow method })
        {
            return;
        }

        var confirmed = await DisplayAlert(
            "Supprimer ce moyen ?",
            $"{method.Label} {method.Reference} ne sera plus proposé pour vos paiements.",
            "Supprimer",
            "Annuler");
        if (!confirmed)
        {
            return;
        }

        ErrorLabel.IsVisible = false;
        var result = await apiClient.DeletePaymentMethodAsync(method.Id);
        if (!result.IsSuccess)
        {
            ErrorLabel.Text = result.ErrorMessage ?? "Ce moyen de paiement n'a pas pu être supprimé.";
            ErrorLabel.IsVisible = true;
            return;
        }

        if (ReferenceEquals(selectedMethod, method))
        {
            selectedMethod = null;
        }

        await LoadAsync();
    }

    private void Select(PaymentMethodRow? row)
    {
        foreach (var item in methods) item.IsSelected = ReferenceEquals(item, row);
        selectedMethod = row;
        ContinueButton.IsEnabled = row is not null;
    }

    private async void OnContinueClicked(object sender, EventArgs e)
    {
        if (selectedMethod is not { } selected) return;
        if (Guid.TryParse(MissionId, out var missionId))
        {
            ContinueButton.IsEnabled = false;
            var result = await apiClient.SelectMissionPaymentMethodAsync(missionId, selected.Id);
            ContinueButton.IsEnabled = true;
            if (!result.IsSuccess)
            {
                ErrorLabel.Text = result.ErrorMessage ?? "Ce moyen de paiement n'a pas pu etre selectionne.";
                ErrorLabel.IsVisible = true;
                return;
            }
            await Shell.Current.GoToAsync($"{nameof(MissionDetailPage)}?missionId={missionId:D}");
            return;
        }
        await Shell.Current.GoToAsync("..");
    }

    private async void OnAddClicked(object sender, EventArgs e) => await GoToAddAsync();
    private async Task GoToAddAsync() => await Shell.Current.GoToAsync($"{nameof(AddPaymentMethodPage)}?missionId={Uri.EscapeDataString(MissionId ?? string.Empty)}");
    private async void OnBackClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    private sealed class PaymentMethodRow : BindableObject
    {
        private bool isSelected;
        private PaymentMethodRow(Guid id, string label, string reference, string iconText, ImageSource? logoSource, bool isDefault)
        { Id = id; Label = label; Reference = reference; IconText = iconText; LogoSource = logoSource; IsDefault = isDefault; }
        public Guid Id { get; }
        public string Label { get; }
        public string Reference { get; }
        public string IconText { get; }
        public ImageSource? LogoSource { get; }
        public bool ShowFallback => LogoSource is null;
        public bool IsDefault { get; }
        public bool IsSelected { get => isSelected; set { if (isSelected == value) return; isSelected = value; OnPropertyChanged(); } }
        public static PaymentMethodRow From(ClientPaymentMethodResponse response, ImageSource? logo) => new(
            response.Id, response.PaymentProviderName ?? response.Label, response.MaskedReference ?? "Compte securise",
            response.Method == "Card" ? "CB" : "MM", logo, response.IsDefault);
    }
}
