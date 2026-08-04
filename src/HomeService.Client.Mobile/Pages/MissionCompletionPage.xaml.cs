using HomeService.Client.Mobile.Services;

namespace HomeService.Client.Mobile.Pages;

[QueryProperty(nameof(MissionId), "missionId")]
public partial class MissionCompletionPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
    private readonly ClientSessionStore sessionStore = MobileServiceLocator.GetRequiredService<ClientSessionStore>();
    private Guid currentMissionId;

    public MissionCompletionPage()
    {
        InitializeComponent();
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
        if (!Guid.TryParse(MissionId, out currentMissionId))
        {
            ShowError("Mission introuvable.");
            return;
        }

        if (sessionStore.IsPreviewMode())
        {
            ProviderNameLabel.Text = "Mohamed Kouyaté et l’équipe Wélé";
            ServiceLabel.Text = "Débouchage d’un évier";
            CompletionMessageLabel.Text = "L’intervention est terminée. Votre retour aidera Mohamed et les prochains clients.";
            return;
        }

        var result = await apiClient.GetMissionAsync(currentMissionId);
        if (!result.IsSuccess || result.Response is null)
        {
            ShowError(result.ErrorMessage ?? "Impossible de charger l’intervention.");
            return;
        }

        var mission = result.Response;
        var providerName = mission.AssignedProvider?.FullName ?? "Votre prestataire";
        ProviderNameLabel.Text = providerName;
        ServiceLabel.Text = string.Join(" · ", new[] { mission.ServiceName, mission.PrestationName }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        if (string.IsNullOrWhiteSpace(ServiceLabel.Text))
        {
            ServiceLabel.Text = "Intervention terminée avec succès";
        }

        CompletionMessageLabel.Text = $"L’intervention est terminée. Votre retour aidera {providerName} et les prochains clients.";
    }

    private async void OnRateClicked(object sender, EventArgs e)
    {
        if (currentMissionId != Guid.Empty)
        {
            await Shell.Current.GoToAsync($"{nameof(MissionRatingPage)}?missionId={currentMissionId:D}");
        }
    }

    private async void OnInvoiceClicked(object sender, EventArgs e)
    {
        if (currentMissionId == Guid.Empty)
        {
            return;
        }

        ErrorLabel.IsVisible = false;
        if (sessionStore.IsPreviewMode())
        {
            await DisplayAlert("Aperçu", "La facture sera disponible ici.", "OK");
            return;
        }

        var result = await apiClient.DownloadMissionInvoiceAsync(currentMissionId);
        if (!result.IsSuccess || result.Response is null)
        {
            ShowError(result.ErrorMessage ?? "La facture n’est pas disponible.");
            return;
        }

        var path = Path.Combine(FileSystem.CacheDirectory, $"facture-wele-{currentMissionId:N}.pdf");
        await File.WriteAllBytesAsync(path, result.Response);
        await Launcher.Default.OpenAsync(new OpenFileRequest("Facture Wélé", new ReadOnlyFile(path, "application/pdf")));
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
