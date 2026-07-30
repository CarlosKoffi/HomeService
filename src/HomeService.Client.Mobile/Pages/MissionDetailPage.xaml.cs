using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;
using System.Collections.ObjectModel;

namespace HomeService.Client.Mobile.Pages;

[QueryProperty(nameof(MissionId), "missionId")]
public partial class MissionDetailPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient;
    private readonly ObservableCollection<AdditionalQuoteRow> additionalQuotes = [];
    private readonly ObservableCollection<PhotoRow> photos = [];
    private readonly ObservableCollection<OfferRow> offers = [];
    private Guid currentMissionId;

    public MissionDetailPage()
    {
        InitializeComponent();
        apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
        AdditionalQuotesView.ItemsSource = additionalQuotes;
        PhotosView.ItemsSource = photos;
        OffersView.ItemsSource = offers;
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
        TrackingLabel.Text = BuildTrackingMessage(mission);
        UpdateProgress(mission);

        ProviderCard.IsVisible = mission.AssignedProvider is not null;
        if (mission.AssignedProvider is not null)
        {
            ProviderLabel.Text = $"{mission.AssignedProvider.FullName} - {mission.AssignedProvider.CompletedMissionCount} intervention(s)";
            ProviderPhoneLabel.Text = mission.ContactDetailsReleased
                ? mission.AssignedProvider.PhoneNumber ?? "Telephone indisponible"
                : "Telephone visible apres confirmation.";
        }

        ConfirmButton.IsVisible = mission.Actions.CanAcceptQuote;
        ConfirmButton.Text = mission.Actions.AmountToPayNow.HasValue
            ? $"Accepter et payer {mission.Actions.AmountToPayNow:N0} {mission.Currency}"
            : "Accepter le devis et payer";
        CompleteButton.IsVisible = mission.Actions.CanValidateCompletion;
        CancelButton.IsVisible = mission.Actions.CanCancel;

        additionalQuotes.Clear();
        foreach (var quote in mission.AdditionalQuotes)
        {
            additionalQuotes.Add(AdditionalQuoteRow.From(quote, mission.Currency));
        }

        photos.Clear();
        foreach (var photo in mission.Photos)
        {
            photos.Add(PhotoRow.From(photo));
        }

        offers.Clear();
        foreach (var offer in mission.CompanyOffers)
        {
            offers.Add(OfferRow.From(offer));
        }

        AdditionalQuotesCard.IsVisible = additionalQuotes.Count > 0;
        PhotosCard.IsVisible = photos.Count > 0;
        OffersCard.IsVisible = offers.Count > 0;
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

    private async void OnAdditionalQuoteSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not AdditionalQuoteRow quote)
        {
            return;
        }

        AdditionalQuotesView.SelectedItem = null;
        if (!quote.CanPay)
        {
            return;
        }

        var accepted = await Shell.Current.DisplayAlert("Devis complementaire", $"Payer {quote.AmountLabel} ?", "Oui", "Non");
        if (!accepted)
        {
            return;
        }

        var result = await apiClient.PayAdditionalQuoteAsync(
            currentMissionId,
            quote.QuoteId,
            $"MOBILE-ADD-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}");

        if (!result.IsSuccess)
        {
            ShowError(result.ErrorMessage);
            return;
        }

        await Shell.Current.DisplayAlert("Paiement confirme", "Le devis complementaire est paye.", "OK");
        await LoadAsync();
    }

    private void ShowError(string? message)
    {
        ErrorLabel.Text = message ?? "Action impossible.";
        ErrorLabel.IsVisible = true;
    }

    private void UpdateProgress(ClientMissionStatusResponse mission)
    {
        var neutral = Color.FromArgb("#F8FAFC");

        StepQuote.BackgroundColor = mission.QuoteStatus is "Submitted" or "Accepted" || mission.CustomerConfirmedAt is not null
            ? Color.FromArgb("#EFF6FF")
            : neutral;
        StepProvider.BackgroundColor = mission.AssignedProvider is not null || mission.ProviderAcceptedAt is not null
            ? Color.FromArgb("#EFF6FF")
            : neutral;
        StepDone.BackgroundColor = mission.Status == "Completed"
            ? Color.FromArgb("#ECFDF5")
            : neutral;
    }

    private static string BuildTrackingMessage(ClientMissionStatusResponse mission)
    {
        if (mission.Status == "Cancelled")
        {
            return "Cette demande est annulee.";
        }

        if (mission.Status == "Completed")
        {
            return mission.CustomerCompletionValidatedAt is null
                ? "Le technicien a termine. Vous pouvez confirmer si tout est conforme."
                : "Mission terminee et confirmee.";
        }

        if (mission.Status == "Started")
        {
            return "La prestation est en cours.";
        }

        if (mission.Status == "OnTheWay")
        {
            return "Votre technicien est en route.";
        }

        if (mission.AssignedProvider is not null)
        {
            return mission.ContactDetailsReleased
                ? "Le contact est visible. Vous pouvez joindre le technicien si besoin."
                : "Un technicien est affecte. Ses coordonnees seront visibles apres confirmation.";
        }

        if (mission.QuoteStatus == "Submitted")
        {
            return "Un prix est disponible. Le paiement confirme la mission.";
        }

        return "Nous cherchons une entreprise disponible pour vous repondre rapidement.";
    }

    private sealed record AdditionalQuoteRow(
        Guid QuoteId,
        string Title,
        string Description,
        string AmountLabel,
        string ActionLabel,
        bool CanPay)
    {
        public static AdditionalQuoteRow From(ClientMissionAdditionalQuoteResponse quote, string fallbackCurrency)
        {
            var currency = string.IsNullOrWhiteSpace(quote.Currency) ? fallbackCurrency : quote.Currency;
            var amount = quote.Amount.HasValue ? $"{quote.Amount:N0} {currency}" : "Prix a venir";
            var action = quote.CanPay ? "Payer" : quote.Status;
            var description = string.IsNullOrWhiteSpace(quote.CompanyDescription)
                ? quote.Reason
                : quote.CompanyDescription;

            return new AdditionalQuoteRow(quote.QuoteId, amount, description, amount, action, quote.CanPay);
        }
    }

    private sealed record PhotoRow(string FileName, string Caption)
    {
        public static PhotoRow From(ClientMissionAttachmentResponse photo)
        {
            return new PhotoRow(photo.OriginalFileName, photo.Caption ?? "Photo de la demande");
        }
    }

    private sealed record OfferRow(string CompanyName, string RankLabel, string Status)
    {
        public static OfferRow From(ClientMissionOfferResponse offer)
        {
            return new OfferRow(offer.CompanyName, $"Priorite {offer.Rank} - score {offer.Score}", offer.Status);
        }
    }
}
