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
    private readonly ObservableCollection<TimelineRow> timeline = [];
    private Guid currentMissionId;
    private string? currentProviderPhoneNumber;

    public MissionDetailPage()
    {
        InitializeComponent();
        apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
        AdditionalQuotesView.ItemsSource = additionalQuotes;
        PhotosView.ItemsSource = photos;
        TimelineView.ItemsSource = timeline;
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
        AddressLabel.Text = mission.ServiceAddress ?? "Adresse à confirmer";
        PriceLabel.Text = mission.CompanyQuotedAmount.HasValue
            ? $"{mission.CompanyQuotedAmount:N0} {mission.Currency}"
            : $"À partir de {mission.StartingPriceAmount:N0} {mission.Currency}";
        PaymentLabel.Text = $"{ResolvePaymentLabel(mission.PaymentMethod)} - {ResolvePaymentStatusLabel(mission.PaymentStatus)}";
        MessageLabel.Text = mission.Message;
        TrackingLabel.Text = BuildTrackingMessage(mission);
        ProviderCard.IsVisible = mission.AssignedProvider is not null;
        currentProviderPhoneNumber = null;
        if (mission.AssignedProvider is not null)
        {
            ProviderLabel.Text = $"{mission.AssignedProvider.FullName} - {mission.AssignedProvider.CompletedMissionCount} intervention(s)";
            ProviderPhoneLabel.Text = mission.ContactDetailsReleased
                ? mission.AssignedProvider.PhoneNumber ?? "Téléphone indisponible"
                : "Téléphone visible après confirmation.";
            ProviderRatingLabel.Text = mission.AssignedProvider.AverageRating.HasValue
                ? $"★ {mission.AssignedProvider.AverageRating:0.0}"
                : "Nouveau";
            ProviderEtaLabel.Text = mission.AssignedProvider.EstimatedArrivalMinutes.HasValue
                ? $"{mission.AssignedProvider.EstimatedArrivalMinutes} min"
                : "ETA à venir";
            currentProviderPhoneNumber = mission.ContactDetailsReleased ? mission.AssignedProvider.PhoneNumber : null;
            CallButton.IsEnabled = !string.IsNullOrWhiteSpace(currentProviderPhoneNumber);
            CallButton.Opacity = CallButton.IsEnabled ? 1 : 0.55;
            var providerPhotoUrl = apiClient.ToAbsoluteMediaUrl(mission.AssignedProvider.PhotoStoragePath);
            ProviderPhoto.Source = providerPhotoUrl;
            ProviderPhoto.IsVisible = !string.IsNullOrWhiteSpace(providerPhotoUrl);
        }

        timeline.Clear();
        foreach (var row in BuildTimeline(mission))
        {
            timeline.Add(row);
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

        AdditionalQuotesCard.IsVisible = additionalQuotes.Count > 0;
        PhotosCard.IsVisible = photos.Count > 0;
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

    private async void OnOpenChatClicked(object sender, EventArgs e)
    {
        if (currentMissionId == Guid.Empty)
        {
            return;
        }

        await Shell.Current.GoToAsync($"//messages?missionId={currentMissionId:D}");
    }

    private async void OnCallClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(currentProviderPhoneNumber))
        {
            return;
        }

        await Launcher.Default.OpenAsync($"tel:{currentProviderPhoneNumber}");
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

    private static IReadOnlyList<TimelineRow> BuildTimeline(ClientMissionStatusResponse mission)
    {
        var status = mission.Status.ToLowerInvariant();
        var quoteSubmitted = mission.CompanyQuotedAt.HasValue || mission.QuoteStatus.Equals("Submitted", StringComparison.OrdinalIgnoreCase);
        var providerAssigned = mission.AssignedProvider is not null;
        var confirmed = mission.CustomerConfirmedAt.HasValue || mission.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase);
        var started = status is "started" or "ontheway" or "completed";
        var completed = status is "completed";

        return
        [
            TimelineRow.Done("Demande envoyée", mission.CreatedAt.ToString("dd/MM HH:mm")),
            quoteSubmitted
                ? TimelineRow.Done("Prix proposé", mission.CompanyQuotedAt?.ToString("dd/MM HH:mm") ?? "Devis disponible")
                : TimelineRow.Pending("Analyse par l'entreprise", "Vous serez notifié dès qu'un prix est proposé."),
            providerAssigned
                ? TimelineRow.Done("Technicien attribué", mission.AssignedProvider!.FullName)
                : TimelineRow.Pending("Technicien à attribuer", "L'entreprise prépare l'intervention."),
            confirmed
                ? TimelineRow.Done("Paiement confirmé", "La mission peut démarrer.")
                : TimelineRow.Pending("Paiement client", "À faire après acceptation du prix."),
            started
                ? TimelineRow.Done(status == "ontheway" ? "Technicien en route" : "Intervention démarrée", mission.AssignedProvider?.EstimatedArrivalMinutes is null ? null : $"{mission.AssignedProvider.EstimatedArrivalMinutes} min")
                : TimelineRow.Pending("Intervention", "En attente du démarrage."),
            completed
                ? TimelineRow.Done("Mission terminée", mission.CustomerCompletionValidatedAt?.ToString("dd/MM HH:mm") ?? "Validation client attendue")
                : TimelineRow.Pending("Fin et avis", "Vous pourrez noter la prestation.")
        ];
    }

    private static string BuildTrackingMessage(ClientMissionStatusResponse mission)
    {
        if (mission.Status == "Cancelled")
        {
            return "Cette demande est annulée.";
        }

        if (mission.Status == "Completed")
        {
            return mission.CustomerCompletionValidatedAt is null
                ? "Le technicien a terminé. Vous pouvez confirmer si tout est conforme."
                : "Mission terminée et confirmée.";
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
                : "Un technicien est affecté. Ses coordonnées seront visibles après confirmation.";
        }

        if (mission.QuoteStatus == "Submitted")
        {
            return "Un prix est disponible. Le paiement confirme la mission.";
        }

        return "Nous cherchons une entreprise disponible pour vous répondre rapidement.";
    }

    private static string ResolvePaymentLabel(string method)
    {
        return method switch
        {
            "Card" => "Carte bancaire",
            "MobileMoney" => "Mobile Money",
            _ => method
        };
    }

    private static string ResolvePaymentStatusLabel(string status)
    {
        return status switch
        {
            "Paid" => "payé",
            "PartiallyPaid" => "partiel",
            "Refunded" => "remboursé",
            _ => "en attente"
        };
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
            var amount = quote.Amount.HasValue ? $"{quote.Amount:N0} {currency}" : "Prix à venir";
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

    private sealed record TimelineRow(string Title, string? Subtitle, Color DotColor, Color TextColor, bool HasSubtitle)
    {
        public static TimelineRow Done(string title, string? subtitle)
        {
            return new TimelineRow(title, subtitle, Color.FromArgb("#2563EB"), Color.FromArgb("#111827"), !string.IsNullOrWhiteSpace(subtitle));
        }

        public static TimelineRow Pending(string title, string? subtitle)
        {
            return new TimelineRow(title, subtitle, Color.FromArgb("#CBD5E1"), Color.FromArgb("#6B7280"), !string.IsNullOrWhiteSpace(subtitle));
        }
    }

}
