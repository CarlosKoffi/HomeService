using HomeService.Client.Mobile.Services;
namespace HomeService.Client.Mobile.Pages;
public partial class ClientSettingsPage : ContentPage
{
    private readonly ClientSessionStore sessionStore = MobileServiceLocator.GetRequiredService<ClientSessionStore>();
    public ClientSettingsPage() => InitializeComponent();
    private async void OnLogoutClicked(object sender, EventArgs e) { await sessionStore.ClearAsync(); await Shell.Current.GoToAsync("//welcome"); }
    private async void OnBackClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
