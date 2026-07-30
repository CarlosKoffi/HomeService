using HomeService.Client.Mobile.Services;

namespace HomeService.Client.Mobile.Pages;

[QueryProperty(nameof(MissionId), "missionId")]
public partial class MissionDetailPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient;
    private Guid currentMissionId;

    public MissionDetailPage()
    {
        InitializeComponent();
        apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
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
        if (!Guid.TryParse(MissionId, out var missionId))
        {
            ErrorLabel.Text = "Mission introuvable.";
            ErrorLabel.IsVisible = true;
            return;
        }

        currentMissionId = missionId;
        var result = await apiClient.GetMissionAsync(missionId);
        if (!result.IsSuccess || result.Response is null)
        {
            ErrorLabel.Text = result.ErrorMessage ?? "Mission introuvable.";
            ErrorLabel.IsVisible = true;
            return;
        }

        var mission = result.Response;
        TitleLabel.Text = mission.MissionNumber;
        StatusLabel.Text = $"{mission.Status} - {mission.QuoteStatus}";
        ServiceLabel.Text = mission.PrestationName is null ? mission.ServiceName : $"{mission.ServiceName} - {mission.PrestationName}";
        AddressLabel.Text = mission.ServiceAddress ?? "Adresse a confirmer";
        PriceLabel.Text = mission.CompanyQuotedAmount.HasValue
            ? $"{mission.CompanyQuotedAmount:N0} {mission.Currency}"
            : $"A partir de {mission.StartingPriceAmount:N0} {mission.Currency}";
        MessageLabel.Text = mission.Message;

        ProviderCard.IsVisible = mission.AssignedProvider is not null;
        if (mission.AssignedProvider is not null)
        {
            ProviderLabel.Text = $"{mission.AssignedProvider.FullName} - {mission.AssignedProvider.CompletedMissionCount} intervention(s)";
            ProviderPhoneLabel.Text = mission.ContactDetailsReleased
                ? mission.AssignedProvider.PhoneNumber ?? "Telephone indisponible"
                : "Telephone visible apres confirmation.";
        }

        ConfirmButton.IsVisible = mission.Actions.CanAcceptQuote;
        CompleteButton.IsVisible = mission.Actions.CanValidateCompletion;
        CancelButton.IsVisible = mission.Actions.CanCancel;
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        var accepted = await Shell.Current.DisplayAlert("Confirmer", "Accepter ce prix et lancer le paiement ?", "Oui", "Non");
        if (!accepted)
        {
            return;
        }

        var result = await apiClient.ConfirmMissionAsync(currentMissionId, $"MOBILE-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}");
        if (!result.IsSuccess)
        {
            ShowError(result.ErrorMessage);
            return;
        }

        await Shell.Current.DisplayAlert("Paiement confirme", "La mission est confirmee.", "OK");
        await LoadAsync();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        var comment = await Shell.Current.DisplayPromptAsync("Annulation", "Pourquoi souhaitez-vous annuler ?", "Annuler la mission", "Retour", "Motif", maxLength: 180);
        if (string.IsNullOrWhiteSpace(comment))
        {
            return;
        }

        var result = await apiClient.CancelMissionAsync(currentMissionId, "CustomerRequest", comment);
        if (!result.IsSuccess)
        {
            ShowError(result.ErrorMessage);
            return;
        }

        await Shell.Current.DisplayAlert("Mission annulee", "Votre demande a ete annulee.", "OK");
        await LoadAsync();
    }

    private async void OnCompleteClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        var comment = await Shell.Current.DisplayPromptAsync("Avis", "Notez rapidement la prestation de 1 a 5.", "Valider", "Retour", "5", keyboard: Keyboard.Numeric, maxLength: 1);
        if (!int.TryParse(comment, out var rating))
        {
            rating = 5;
        }

        rating = Math.Clamp(rating, 1, 5);
        var result = await apiClient.ValidateCompletionAsync(currentMissionId, rating, "Validation depuis l'application client.");
        if (!result.IsSuccess)
        {
            ShowError(result.ErrorMessage);
            return;
        }

        await Shell.Current.DisplayAlert("Merci", "Mission terminee et avis enregistre.", "OK");
        await LoadAsync();
    }

    private void ShowError(string? message)
    {
        ErrorLabel.Text = message ?? "Action impossible.";
        ErrorLabel.IsVisible = true;
    }
}
