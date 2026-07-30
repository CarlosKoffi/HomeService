using System.Collections.ObjectModel;
using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

public partial class ProfilePage : ContentPage
{
    private readonly ClientMobileApiClient apiClient;
    private readonly ClientSessionStore sessionStore;
    private readonly ObservableCollection<ClientAddressResponse> addresses = [];
    private readonly ObservableCollection<ClientPaymentMethodResponse> payments = [];

    public ProfilePage()
    {
        InitializeComponent();
        apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
        sessionStore = MobileServiceLocator.GetRequiredService<ClientSessionStore>();
        AddressesView.ItemsSource = addresses;
        PaymentsView.ItemsSource = payments;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        LoggedOutCard.IsVisible = !sessionStore.HasSession();
        LoggedInSection.IsVisible = sessionStore.HasSession();

        if (!sessionStore.HasSession())
        {
            return;
        }

        var me = await apiClient.GetMeAsync();
        if (me.IsSuccess && me.Response is not null)
        {
            NameLabel.Text = $"{me.Response.FirstName} {me.Response.LastName}";
            PhoneLabel.Text = me.Response.PhoneNumber;
            EmailLabel.Text = me.Response.Email ?? "Email non renseigne";
        }

        addresses.Clear();
        var addressResult = await apiClient.GetAddressesAsync();
        if (addressResult.IsSuccess && addressResult.Response is not null)
        {
            foreach (var address in addressResult.Response)
            {
                addresses.Add(address);
            }
        }

        payments.Clear();
        var paymentResult = await apiClient.GetPaymentMethodsAsync();
        if (paymentResult.IsSuccess && paymentResult.Response is not null)
        {
            foreach (var payment in paymentResult.Response)
            {
                payments.Add(payment);
            }
        }
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(LoginPage));
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(RegisterPage));
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        await sessionStore.ClearAsync();
        await Shell.Current.GoToAsync("//home");
    }
}
