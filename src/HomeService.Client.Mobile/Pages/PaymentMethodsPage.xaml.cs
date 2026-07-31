using System.Collections.ObjectModel;
using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

[QueryProperty(nameof(MissionId), "missionId")]
public partial class PaymentMethodsPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient;
    private readonly ObservableCollection<PaymentMethodRow> methods = [];

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

        foreach (var method in result.Response)
        {
            methods.Add(PaymentMethodRow.From(method));
        }

        EmptyState.IsVisible = methods.Count == 0;
        if (methods.Count == 0 && Guid.TryParse(MissionId, out _))
        {
            await GoToAddAsync();
            return;
        }

        var defaultMethod = methods.FirstOrDefault(item => item.IsDefault) ?? methods.FirstOrDefault();
        MethodsView.SelectedItem = defaultMethod;
        ContinueButton.IsEnabled = defaultMethod is not null;
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ContinueButton.IsEnabled = e.CurrentSelection.FirstOrDefault() is PaymentMethodRow;

    private async void OnContinueClicked(object sender, EventArgs e)
    {
        if (MethodsView.SelectedItem is not PaymentMethodRow selected)
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

    private async Task GoToAddAsync() =>
        await Shell.Current.GoToAsync($"{nameof(AddPaymentMethodPage)}?missionId={Uri.EscapeDataString(MissionId ?? string.Empty)}");

    private async void OnBackClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    private sealed record PaymentMethodRow(Guid Id, string Label, string Reference, string IconText, string StatusText, bool IsDefault)
    {
        public static PaymentMethodRow From(ClientPaymentMethodResponse response) => new(
            response.Id,
            response.Label,
            response.MaskedReference ?? "Compte securise",
            response.Method == "Card" ? "CB" : "MM",
            response.IsDefault ? "Par defaut" : "›",
            response.IsDefault);
    }
}
