namespace HomeService.Client.Mobile.Pages;

[QueryProperty(nameof(MissionId), "missionId")]
public partial class MissionReviewSuccessPage : ContentPage
{
    public MissionReviewSuccessPage()
    {
        InitializeComponent();
    }

    public string? MissionId { get; set; }

    private async void OnFinishClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//requests");
    }
}
