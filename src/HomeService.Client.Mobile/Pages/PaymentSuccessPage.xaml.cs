using HomeService.Client.Mobile.Services;

namespace HomeService.Client.Mobile.Pages;

[QueryProperty(nameof(MissionId), "missionId")]
public partial class PaymentSuccessPage : ContentPage
{
    private readonly ClientMobileApiClient api = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
    public PaymentSuccessPage() => InitializeComponent();
    public string? MissionId { get; set; }

    private async void OnInvoiceClicked(object sender, EventArgs e)
    {
        if (!Guid.TryParse(MissionId, out var missionId)) return;
        var result = await api.DownloadMissionInvoiceAsync(missionId);
        if (!result.IsSuccess || result.Response is null)
        {
            ErrorLabel.Text = result.ErrorMessage;
            ErrorLabel.IsVisible = true;
            return;
        }

        var path = Path.Combine(FileSystem.CacheDirectory, $"facture-wele-{missionId:N}.pdf");
        await File.WriteAllBytesAsync(path, result.Response);
        await Launcher.Default.OpenAsync(new OpenFileRequest("Facture Wélé", new ReadOnlyFile(path, "application/pdf")));
    }

    private async void OnHomeClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("//home");
}
