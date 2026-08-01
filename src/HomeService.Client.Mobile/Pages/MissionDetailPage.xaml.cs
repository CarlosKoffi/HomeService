using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;
using System.Collections.ObjectModel;

namespace HomeService.Client.Mobile.Pages;

[QueryProperty(nameof(MissionId), "missionId")]
public partial class MissionDetailPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient;
    private readonly ClientSessionStore sessionStore;
    private readonly ObservableCollection<AdditionalQuoteRow> additionalQuotes = [];
    private readonly ObservableCollection<PhotoRow> photos = [];
    private readonly ObservableCollection<TimelineRow> timeline = [];
    private Guid currentMissionId;
    private string? currentProviderPhoneNumber;

    public MissionDetailPage()
    {
        InitializeComponent();
        apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
        sessionStore = MobileServiceLocator.GetRequiredService<ClientSessionStore>();
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
        OverviewPanel.IsVisible = true;
        DetailPanel.IsVisible = false;
        ProviderDetailPanel.IsVisible = false;
        if (!Guid.TryParse(MissionId, out var missionId))
        {
            if (sessionStore.IsPreviewMode())
            {
                BindPreviewMission(Guid.Parse("11111111-1111-1111-1111-111111111111"));
                return;
            }

            ShowError("Mission introuvable.");
            return;
        }

        currentMissionId = missionId;
        if (sessionStore.IsPreviewMode())
        {
            BindPreviewMission(missionId);
            return;
        }

        var result = await apiClient.GetMissionAsync(missionId);
        if (!result.IsSuccess || result.Response is null)
        {
            ErrorLabel.Text = result.ErrorMessage ?? "Mission introuvable.";
            ErrorLabel.IsVisible = true;
            return;
        }

        var mission = result.Response;
        TitleLabel.Text = mission.MissionNumber;
        StatusLabel.Text = ResolveCustomerStatusLabel(mission);
        ServiceLabel.Text = mission.PrestationName is null ? mission.ServiceName : $"{mission.ServiceName} - {mission.PrestationName}";
        AddressLabel.Text = mission.ServiceAddress ?? "Adresse à confirmer";
        DateCaptionLabel.Text = mission.ScheduledFor.HasValue ? "Date du rendez-vous" : "Demande envoyée le";
        DateLabel.Text = (mission.ScheduledFor ?? mission.CreatedAt).ToLocalTime().ToString("dd/MM/yyyy 'à' HH:mm");
        PriceLabel.Text = mission.CompanyQuotedAmount.HasValue
            ? $"{mission.CompanyQuotedAmount:N0} {mission.Currency}"
            : $"À partir de {mission.StartingPriceAmount:N0} {mission.Currency}";
        PaymentLabel.Text = mission.CustomerPaymentMethodId.HasValue
            ? $"{mission.CustomerPaymentMethodLabel} {mission.CustomerPaymentMaskedReference} - {ResolvePaymentStatusLabel(mission.PaymentStatus)}"
            : "A choisir";
        ChoosePaymentButton.IsVisible = mission.Actions.RequiresPaymentMethod;
        MessageLabel.Text = mission.Message;
        TrackingLabel.Text = BuildTrackingMessage(mission);
        var providerHasAccepted = mission.AssignedProvider is not null
            && (mission.ProviderAcceptedAt.HasValue
                || mission.Status is "Accepted" or "OnTheWay" or "Started" or "Completed");
        ProviderCard.IsVisible = providerHasAccepted;
        WaitingLabel.IsVisible = !providerHasAccepted;
        RoutePanel.IsVisible = providerHasAccepted && mission.Status == "OnTheWay";
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
            RouteEtaLabel.Text = mission.AssignedProvider.EstimatedArrivalMinutes.HasValue
                ? $"Arrivee estimee dans {mission.AssignedProvider.EstimatedArrivalMinutes} min"
                : "Arrivee estimee en cours de calcul";
            currentProviderPhoneNumber = mission.ContactDetailsReleased ? mission.AssignedProvider.PhoneNumber : null;
            CallButton.IsEnabled = !string.IsNullOrWhiteSpace(currentProviderPhoneNumber);
            CallButton.Opacity = CallButton.IsEnabled ? 1 : 0.55;
            ProviderPhoto.Source = await apiClient.DownloadMediaImageSourceAsync(mission.AssignedProvider.PhotoStoragePath);
            ProviderPhoto.IsVisible = ProviderPhoto.Source is not null;
            ProviderDetailLabel.Text = $"{mission.AssignedProvider.FullName}\n{ServiceLabel.Text}\n{AddressLabel.Text}\n{ProviderEtaLabel.Text}";
        }

        timeline.Clear();
        foreach (var row in BuildTimeline(mission))
        {
            timeline.Add(row);
        }

        ConfirmButton.IsVisible = mission.Actions.CanAcceptQuote;
        ConfirmButton.Text = mission.Actions.AmountToPayNow.HasValue
            ? $"Accepter et payer {mission.Actions.AmountToPayNow:N0} {mission.Currency}"
            : "Accepter et payer";
        CompleteButton.IsVisible = mission.Actions.CanValidateCompletion;
        CancelButton.IsVisible = mission.Actions.CanCancel;
        BindOverviewAction(mission);

        additionalQuotes.Clear();
        foreach (var quote in mission.AdditionalQuotes)
        {
            additionalQuotes.Add(AdditionalQuoteRow.From(quote, mission.Currency));
        }

        photos.Clear();
        foreach (var photo in mission.Photos)
        {
            var preview = await apiClient.DownloadMissionAttachmentImageSourceAsync(missionId, photo.AttachmentId);
            photos.Add(PhotoRow.From(photo, preview));
        }

        AdditionalQuotesCard.IsVisible = additionalQuotes.Count > 0;
        PhotosCard.IsVisible = photos.Count > 0;
    }

    private void BindPreviewMission(Guid missionId)
    {
        currentMissionId = missionId;
        ErrorLabel.IsVisible = false;
        TitleLabel.Text = "WL-000145";
        StatusLabel.Text = "En cours";
        ServiceLabel.Text = "Déboucher un évier";
        AddressLabel.Text = "Cocody, Riviera 3";
        DateCaptionLabel.Text = "Date du rendez-vous";
        DateLabel.Text = "Aujourd'hui à 14:30";
        PriceLabel.Text = "17 000 FCFA";
        PaymentLabel.Text = "Mobile Money - payé";
        MessageLabel.Text = "L'eau s'écoule sous l'évier depuis ce matin.";
        TrackingLabel.Text = "Mohamed est affecté à votre demande. Vous pouvez le contacter si besoin.";

        ProviderCard.IsVisible = true;
        WaitingLabel.IsVisible = false;
        RoutePanel.IsVisible = true;
        ProviderLabel.Text = "Mohamed Kouyaté - 48 interventions";
        ProviderPhoneLabel.Text = "+225 07 12 34 56 78";
        ProviderRatingLabel.Text = "★ 4.9";
        ProviderEtaLabel.Text = "13 min";
        RouteEtaLabel.Text = "Arrivee estimee dans 13 min";
        ProviderDetailLabel.Text = "Mohamed Kouyate\nDeboucher un evier\nCocody, Riviera 3\nArrivee dans 13 min";
        currentProviderPhoneNumber = "+2250712345678";
        CallButton.IsEnabled = true;
        CallButton.Opacity = 1;
        ProviderPhoto.IsVisible = false;

        timeline.Clear();
        foreach (var row in new[]
        {
            TimelineRow.Done("Demande envoyée", "Aujourd'hui 10:30"),
            TimelineRow.Done("Prix proposé", "17 000 FCFA"),
            TimelineRow.Done("Technicien attribué", "Mohamed Kouyaté"),
            TimelineRow.Done("Paiement confirmé", "Mission confirmée"),
            TimelineRow.Done("Technicien en route", "Arrivée dans 13 min"),
            TimelineRow.Pending("Fin et avis", "Vous pourrez noter la prestation.")
        })
        {
            timeline.Add(row);
        }

        additionalQuotes.Clear();
        photos.Clear();
        photos.Add(new PhotoRow("Photo du problème", null, true));
        PhotosCard.IsVisible = true;
        AdditionalQuotesCard.IsVisible = false;
        ConfirmButton.IsVisible = false;
        CompleteButton.IsVisible = true;
        CancelButton.IsVisible = true;
        OverviewActionCard.IsVisible = true;
        OverviewActionCaption.Text = "Intervention terminée";
        OverviewActionTitle.Text = "Tout s'est bien passé ?";
        OverviewActionAmount.IsVisible = false;
        OverviewActionHelp.Text = "Confirmez la fin de la prestation et laissez votre avis.";
        OverviewConfirmButton.IsVisible = false;
        OverviewChoosePaymentButton.IsVisible = false;
        OverviewCompleteButton.IsVisible = true;
    }

    private void BindOverviewAction(ClientMissionStatusResponse mission)
    {
        OverviewActionCard.IsVisible = false;
        OverviewActionAmount.IsVisible = false;
        OverviewConfirmButton.IsVisible = false;
        OverviewChoosePaymentButton.IsVisible = false;
        OverviewCompleteButton.IsVisible = false;

        if (mission.Actions.RequiresPaymentMethod)
        {
            OverviewActionCard.IsVisible = true;
            OverviewActionCaption.Text = "Paiement";
            OverviewActionTitle.Text = "Choisissez votre moyen de paiement";
            OverviewActionHelp.Text = "Il sera utilisé uniquement après votre validation du prix.";
            OverviewChoosePaymentButton.IsVisible = true;
            return;
        }

        if (mission.Actions.CanAcceptQuote)
        {
            var amount = mission.Actions.AmountToPayNow ?? mission.CompanyQuotedAmount;
            OverviewActionCard.IsVisible = true;
            OverviewActionCaption.Text = "Prix proposé";
            OverviewActionTitle.Text = "L'entreprise a confirmé le prix de l'intervention";
            OverviewActionAmount.IsVisible = amount.HasValue;
            OverviewActionAmount.Text = amount.HasValue ? $"{amount:N0} {mission.Currency}" : string.Empty;
            OverviewActionHelp.Text = "Vérifiez le montant, puis confirmez pour réserver le technicien et payer.";
            OverviewConfirmButton.Text = amount.HasValue
                ? $"Accepter et payer {amount:N0} {mission.Currency}"
                : "Accepter et payer";
            OverviewConfirmButton.IsVisible = true;
            return;
        }

        if (mission.Actions.CanValidateCompletion)
        {
            OverviewActionCard.IsVisible = true;
            OverviewActionCaption.Text = "Intervention terminée";
            OverviewActionTitle.Text = "Tout s'est bien passé ?";
            OverviewActionHelp.Text = "Confirmez la fin de la prestation et laissez votre avis.";
            OverviewCompleteButton.IsVisible = true;
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private void OnShowDetailsClicked(object sender, EventArgs e)
    {
        OverviewPanel.IsVisible = false;
        DetailPanel.IsVisible = true;
    }

    private void OnProviderTapped(object sender, TappedEventArgs e)
    {
        ProviderDetailPanel.IsVisible = !ProviderDetailPanel.IsVisible;
    }

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        var accepted = await Shell.Current.DisplayAlert("Confirmer", "Accepter ce prix et lancer le paiement ?", "Oui", "Non");
        if (!accepted)
        {
            return;
        }

        if (sessionStore.IsPreviewMode())
        {
            await Shell.Current.DisplayAlert("Aperçu", "Paiement simulé.", "OK");
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

    private async void OnChoosePaymentClicked(object sender, EventArgs e)
    {
        if (currentMissionId != Guid.Empty)
        {
            await Shell.Current.GoToAsync($"{nameof(PaymentMethodsPage)}?missionId={currentMissionId:D}");
        }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        var comment = await Shell.Current.DisplayPromptAsync("Annulation", "Pourquoi souhaitez-vous annuler ?", "Annuler la mission", "Retour", "Motif", maxLength: 180);
        if (string.IsNullOrWhiteSpace(comment))
        {
            return;
        }

        if (sessionStore.IsPreviewMode())
        {
            await Shell.Current.DisplayAlert("Aperçu", "Annulation simulée.", "OK");
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
        if (sessionStore.IsPreviewMode())
        {
            await Shell.Current.DisplayAlert("Merci", "Avis simulé en mode aperçu.", "OK");
            return;
        }

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

        await Shell.Current.GoToAsync($"{nameof(MissionChatPage)}?missionId={currentMissionId:D}");
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

        if (sessionStore.IsPreviewMode())
        {
            await Shell.Current.DisplayAlert("Aperçu", "Paiement complémentaire simulé.", "OK");
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
        var companyReviewStarted = mission.AssignedCompany is not null;
        var providerAssigned = mission.AssignedProvider is not null;
        var confirmed = mission.CustomerConfirmedAt.HasValue || mission.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase);
        var started = status is "started" or "ontheway" or "completed";
        var completed = status is "completed";

        return
        [
            TimelineRow.Done("Demande envoyée", mission.CreatedAt.ToString("dd/MM HH:mm")),
            quoteSubmitted
                ? TimelineRow.Done("Prix proposé", mission.CompanyQuotedAt?.ToString("dd/MM HH:mm") ?? "Devis disponible")
                : companyReviewStarted
                    ? TimelineRow.Done("Entreprise en cours d'analyse", mission.AssignedCompany!.Name)
                    : TimelineRow.Pending("Recherche d'une entreprise", "Votre demande est proposée aux entreprises disponibles."),
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

        if (mission.AssignedCompany is not null)
        {
            return $"{mission.AssignedCompany.Name} analyse votre demande et prépare l'affectation d'un technicien.";
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

    private static string ResolveCustomerStatusLabel(ClientMissionStatusResponse mission)
    {
        if (mission.AssignedCompany is not null
            && mission.AssignedProvider is null
            && mission.Status is "Requested" or "SearchingProvider" or "Offered")
        {
            return "Analyse par l'entreprise";
        }

        return mission.Status switch
        {
            "Requested" or "SearchingProvider" => "Recherche d'une entreprise disponible",
            "Quoted" => "Prix proposé par l'entreprise",
            "Accepted" => "Technicien affecté",
            "OnTheWay" => "Technicien en route",
            "Started" => "Intervention en cours",
            "Completed" => "Intervention terminée",
            "Cancelled" => "Demande annulée",
            _ => "Demande en cours de traitement"
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

    private sealed record PhotoRow(string Caption, ImageSource? PreviewSource, bool IsPreviewMissing)
    {
        public static PhotoRow From(ClientMissionAttachmentResponse photo, ImageSource? previewSource)
        {
            return new PhotoRow(photo.Caption ?? "Photo de la demande", previewSource, previewSource is null);
        }
    }

    private sealed record TimelineRow(string Title, string? Subtitle, string TimeLabel, Color DotColor, Color TextColor, bool HasSubtitle)
    {
        public static TimelineRow Done(string title, string? subtitle)
        {
            var time = ExtractTime(subtitle);
            return new TimelineRow(title, subtitle, time, Color.FromArgb("#2563EB"), Color.FromArgb("#111827"), !string.IsNullOrWhiteSpace(subtitle));
        }

        public static TimelineRow Pending(string title, string? subtitle)
        {
            return new TimelineRow(title, subtitle, string.Empty, Color.FromArgb("#CBD5E1"), Color.FromArgb("#6B7280"), !string.IsNullOrWhiteSpace(subtitle));
        }

        private static string ExtractTime(string? value)
            => DateTimeOffset.TryParse(value, out var parsed) ? parsed.ToString("HH:mm") : string.Empty;
    }

}
