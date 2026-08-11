using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;
using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Maui.ApplicationModel;

namespace HomeService.Client.Mobile.Pages;

[QueryProperty(nameof(MissionId), "missionId")]
public partial class MissionDetailPage : ContentPage
{
    private static readonly TimeSpan AutoRefreshInterval = TimeSpan.FromSeconds(15);
    private readonly ClientMobileApiClient apiClient;
    private readonly ClientSessionStore sessionStore;
    private readonly ObservableCollection<AdditionalQuoteRow> additionalQuotes = [];
    private readonly ObservableCollection<PhotoRow> photos = [];
    private readonly ObservableCollection<TimelineRow> timeline = [];
    private readonly SemaphoreSlim loadGate = new(1, 1);
    private Guid currentMissionId;
    private string? currentProviderPhoneNumber;
    private ClientMissionProviderResponse? currentTrackingProvider;
    private CancellationTokenSource? autoRefreshCancellation;
    private CancellationTokenSource? actionCountdownCancellation;
    private string? loadedProviderPhotoPath;
    private bool isOpeningChat;

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
        isOpeningChat = false;
        StopAutoRefresh();
        autoRefreshCancellation = new CancellationTokenSource();
        var cancellationToken = autoRefreshCancellation.Token;

        try
        {
            await LoadAsync(cancellationToken);
            _ = RunAutoRefreshAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal when the user leaves the mission screen during a refresh.
        }
    }

    protected override void OnDisappearing()
    {
        StopAutoRefresh();
        base.OnDisappearing();
    }

    private async Task RunAutoRefreshAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(AutoRefreshInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    await LoadAsync(cancellationToken, preserveViewState: true, includeMedia: false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    // A later timer tick retries after a transient network failure.
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal when the page is no longer visible.
        }
    }

    private void StopAutoRefresh()
    {
        StopActionCountdown();
        var cancellation = autoRefreshCancellation;
        autoRefreshCancellation = null;
        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        cancellation.Dispose();
    }

    private async Task LoadAsync(
        CancellationToken cancellationToken = default,
        bool preserveViewState = false,
        bool includeMedia = true)
    {
        await loadGate.WaitAsync(cancellationToken);
        try
        {
            ErrorLabel.IsVisible = false;
            if (!preserveViewState)
            {
                OverviewPanel.IsVisible = true;
                DetailPanel.IsVisible = false;
                ProviderDetailPanel.IsVisible = false;
            }

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

            var result = await apiClient.GetMissionAsync(missionId, cancellationToken);
            if (!result.IsSuccess || result.Response is null)
            {
                if (!preserveViewState)
                {
                    ErrorLabel.Text = result.ErrorMessage ?? "Mission introuvable.";
                    ErrorLabel.IsVisible = true;
                }
                return;
            }

            var mission = result.Response;
        var providerHasAccepted = mission.AssignedProvider is not null
            && (mission.ProviderAcceptedAt.HasValue
                || mission.Status is "Accepted" or "OnTheWay" or "Started" or "Completed");
        var conversationIsActive = IsConversationActive(mission.Status);
        TitleLabel.Text = mission.MissionNumber;
        StatusLabel.Text = ResolveCustomerStatusLabel(mission);
        ServiceLabel.Text = mission.ServiceName ?? "Service";
        PrestationLabel.Text = mission.PrestationName ?? string.Empty;
        PrestationPanel.IsVisible = !string.IsNullOrWhiteSpace(mission.PrestationName);
        OptionLabel.Text = mission.OptionName ?? string.Empty;
        OptionPanel.IsVisible = !string.IsNullOrWhiteSpace(mission.OptionName);
        AddressLabel.Text = mission.ServiceAddress ?? "Adresse à confirmer";
        DateCaptionLabel.Text = mission.ScheduledFor.HasValue ? "Date du rendez-vous" : "Demande envoyée le";
        DateLabel.Text = mission.ScheduledFor.HasValue
            ? AppointmentDisplayFormatter.FormatWindow(mission.ScheduledFor.Value, "dd/MM/yyyy")
            : mission.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy 'à' HH:mm");
        PricePaymentPanel.IsVisible = providerHasAccepted;
        PriceLabel.Text = providerHasAccepted
            ? $"{(mission.CompanyQuotedAmount ?? mission.EstimatedTotalAmount ?? mission.ServiceAmount):N0} {mission.Currency}"
            : string.Empty;
        PaymentLabel.Text = mission.CustomerPaymentMethodId.HasValue
            ? $"{mission.CustomerPaymentMethodLabel} {mission.CustomerPaymentMaskedReference} - {ResolvePaymentStatusLabel(mission.PaymentStatus)}"
            : "A choisir";
        ChoosePaymentButton.IsVisible = mission.Actions.RequiresPaymentMethod;
        MessageLabel.Text = mission.Message;
        TrackingLabel.Text = BuildTrackingMessage(mission);
        ProviderCard.IsVisible = providerHasAccepted;
        ProviderChatButton.IsVisible = conversationIsActive;
        ProviderDetailChatButton.IsVisible = conversationIsActive;
        WaitingLabel.IsVisible = !providerHasAccepted;
        RoutePanel.IsVisible = providerHasAccepted;
        currentProviderPhoneNumber = null;
        currentTrackingProvider = null;
        if (mission.AssignedProvider is not null)
        {
            currentTrackingProvider = mission.AssignedProvider;
            var experienceLabel = mission.AssignedProvider.CompletedMissionCount < 10
                ? "D\u00e9bute"
                : $"{mission.AssignedProvider.CompletedMissionCount} interventions";
            ProviderLabel.Text = $"{mission.AssignedProvider.FullName} - {experienceLabel}";
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
            TrackProviderTitleLabel.Text = $"Voir où se trouve {mission.AssignedProvider.FullName}";
            RouteEtaLabel.Text = mission.AssignedProvider.CanTrackLocation
                ? $"{mission.AssignedProvider.DistanceKm:0.0} km · arrivée dans {mission.AssignedProvider.EstimatedArrivalMinutes} min environ"
                : "Position en cours de mise à jour";
            RoutePanel.Opacity = mission.AssignedProvider.CanTrackLocation ? 1 : 0.65;
            currentProviderPhoneNumber = mission.ContactDetailsReleased ? mission.AssignedProvider.PhoneNumber : null;
            CallButton.IsEnabled = !string.IsNullOrWhiteSpace(currentProviderPhoneNumber);
            CallButton.Opacity = CallButton.IsEnabled ? 1 : 0.55;
            if (includeMedia || !string.Equals(loadedProviderPhotoPath, mission.AssignedProvider.PhotoStoragePath, StringComparison.Ordinal))
            {
                ProviderPhoto.Source = await apiClient.DownloadProfilePhotoAsync(
                    mission.AssignedProvider.PhotoStoragePath,
                    cancellationToken);
                ProviderPhoto.IsVisible = ProviderPhoto.Source is not null;
                loadedProviderPhotoPath = mission.AssignedProvider.PhotoStoragePath;
            }
            var missionSelection = string.Join(" - ", new[] { mission.ServiceName, mission.PrestationName, mission.OptionName }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            ProviderDetailLabel.Text = $"{mission.AssignedProvider.FullName}\n{missionSelection}\n{AddressLabel.Text}\n{ProviderEtaLabel.Text}";
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

            if (includeMedia)
            {
                photos.Clear();
                foreach (var photo in mission.Photos)
                {
                    var preview = await apiClient.DownloadMissionAttachmentImageSourceAsync(
                        missionId,
                        photo.AttachmentId,
                        cancellationToken);
                    photos.Add(PhotoRow.From(photo, preview));
                }
            }

            AdditionalQuotesCard.IsVisible = additionalQuotes.Count > 0;
            PhotosCard.IsVisible = photos.Count > 0;
        }
        finally
        {
            loadGate.Release();
        }
    }

    private void BindPreviewMission(Guid missionId)
    {
        currentMissionId = missionId;
        ErrorLabel.IsVisible = false;
        TitleLabel.Text = "WL-000145";
        StatusLabel.Text = "En cours";
        ServiceLabel.Text = "Plomberie";
        PrestationLabel.Text = "Déboucher un évier";
        PrestationPanel.IsVisible = true;
        OptionLabel.Text = string.Empty;
        OptionPanel.IsVisible = false;
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
        RoutePanel.Opacity = 1;
        ProviderLabel.Text = "Mohamed Kouyaté - 48 interventions";
        ProviderPhoneLabel.Text = "+225 07 12 34 56 78";
        ProviderRatingLabel.Text = "★ 4.9";
        ProviderEtaLabel.Text = "13 min";
        TrackProviderTitleLabel.Text = "Voir où se trouve Mohamed Kouyaté";
        RouteEtaLabel.Text = "2,1 km · arrivée dans 13 min environ";
        ProviderDetailLabel.Text = "Mohamed Kouyate\nDeboucher un evier\nCocody, Riviera 3\nArrivee dans 13 min";
        currentProviderPhoneNumber = "+2250712345678";
        CallButton.IsEnabled = true;
        CallButton.Opacity = 1;
        ProviderPhoto.IsVisible = false;

        timeline.Clear();
        foreach (var row in new[]
        {
            TimelineRow.Done("Demande envoyée", "Aujourd'hui 10:30"),
            TimelineRow.Done("Votre prestataire est prêt", "Mohamed Kouyaté"),
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
        CancelButton.IsVisible = false;
        OverviewActionCard.IsVisible = true;
        OverviewActionCaption.Text = "Intervention terminée";
        OverviewActionTitle.Text = "Tout s'est bien passé ?";
        OverviewActionAmount.IsVisible = false;
        OverviewPriceBreakdown.IsVisible = false;
        OverviewActionHelp.Text = "Confirmez la fin de la prestation et laissez votre avis.";
        OverviewActionCountdownLabel.IsVisible = false;
        OverviewConfirmButton.IsVisible = false;
        OverviewChoosePaymentButton.IsVisible = false;
        OverviewCompleteButton.IsVisible = true;
    }

    private void BindOverviewAction(ClientMissionStatusResponse mission)
    {
        StopActionCountdown();
        OverviewActionCard.IsVisible = false;
        OverviewActionAmount.IsVisible = false;
        OverviewPriceBreakdown.IsVisible = false;
        OverviewActionCountdownLabel.IsVisible = false;
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
            StartActionCountdown(mission.CustomerPaymentExpiresAt, "Choisissez votre moyen de paiement avant");
            return;
        }

        if (mission.Actions.CanAcceptQuote)
        {
            var amount = mission.Actions.AmountToPayNow ?? mission.CompanyQuotedAmount;
            var serviceAmount = mission.CompanyQuotedAmount
                ?? mission.EstimatedTotalAmount
                ?? mission.ServiceAmount;
            var totalAmount = mission.CustomerTotalAmount > 0
                ? mission.CustomerTotalAmount
                : amount ?? serviceAmount + mission.CustomerServiceFeeAmount;
            OverviewActionCard.IsVisible = true;
            OverviewActionCaption.Text = "Action requise";
            OverviewActionTitle.Text = "Votre prestataire est prêt";
            OverviewPriceBreakdown.IsVisible = true;
            OverviewServiceAmountLabel.Text = $"{serviceAmount:N0} {mission.Currency}";
            OverviewServiceFeeLabel.Text = $"Frais de service ({mission.CustomerServiceFeeRateBasisPoints / 100m:0.##} %)";
            OverviewServiceFeeAmountLabel.Text = $"{mission.CustomerServiceFeeAmount:N0} {mission.Currency}";
            OverviewTotalAmountLabel.Text = $"{totalAmount:N0} {mission.Currency}";
            OverviewActionHelp.Text = "Vérifiez le montant, puis payez pour confirmer la mission et autoriser le départ du prestataire.";
            OverviewConfirmButton.Text = totalAmount > 0
                ? $"Accepter et payer {totalAmount:N0} {mission.Currency}"
                : "Accepter et payer";
            OverviewConfirmButton.IsVisible = true;
            StartActionCountdown(mission.CustomerPaymentExpiresAt, "Confirmation automatique dans");
            return;
        }

        if (mission.Actions.CanValidateCompletion)
        {
            OverviewActionCard.IsVisible = true;
            OverviewActionCaption.Text = "Intervention terminée";
            OverviewActionTitle.Text = "Tout s'est bien passé ?";
            OverviewActionHelp.Text = "Confirmez la fin de la prestation et laissez votre avis. Sans action, la mission sera validée automatiquement.";
            OverviewCompleteButton.IsVisible = true;
            StartActionCountdown(mission.CustomerCompletionValidationExpiresAt, "Validation automatique dans");
        }
    }

    private void StartActionCountdown(DateTimeOffset? expiresAt, string prefix)
    {
        if (!expiresAt.HasValue)
        {
            return;
        }

        OverviewActionCountdownLabel.IsVisible = true;
        UpdateActionCountdown(expiresAt.Value, prefix);
        if (expiresAt.Value <= DateTimeOffset.UtcNow)
        {
            return;
        }

        actionCountdownCancellation = new CancellationTokenSource();
        _ = RunActionCountdownAsync(expiresAt.Value, prefix, actionCountdownCancellation.Token);
    }

    private async Task RunActionCountdownAsync(
        DateTimeOffset expiresAt,
        string prefix,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var expired = expiresAt <= DateTimeOffset.UtcNow;
                await MainThread.InvokeOnMainThreadAsync(() => UpdateActionCountdown(expiresAt, prefix));
                if (!expired)
                {
                    continue;
                }

                await LoadAsync(cancellationToken, preserveViewState: true, includeMedia: false);
                return;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal when the action changes or the page is left.
        }
    }

    private void UpdateActionCountdown(DateTimeOffset expiresAt, string prefix)
    {
        var remaining = expiresAt - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            OverviewActionCountdownLabel.Text = "Délai écoulé · mise à jour en cours";
            return;
        }

        var totalHours = (int)remaining.TotalHours;
        OverviewActionCountdownLabel.Text = totalHours > 0
            ? $"{prefix} {totalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}"
            : $"{prefix} {remaining.Minutes:00}:{remaining.Seconds:00}";
    }

    private void StopActionCountdown()
    {
        actionCountdownCancellation?.Cancel();
        actionCountdownCancellation?.Dispose();
        actionCountdownCancellation = null;
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

    private async void OnTrackProviderTapped(object sender, TappedEventArgs e)
    {
        if (sessionStore.IsPreviewMode())
        {
            await NavigateProviderTrackingAsync(currentMissionId, "Mohamed Kouyaté", 5.3478m, -4.0203m, 5.3599m, -4.0083m, 13, 2.1m);
            return;
        }

        var provider = currentTrackingProvider;
        if (provider is null
            || !provider.CanTrackLocation
            || !provider.CurrentLatitude.HasValue
            || !provider.CurrentLongitude.HasValue
            || !provider.DestinationLatitude.HasValue
            || !provider.DestinationLongitude.HasValue)
        {
            await DisplayAlert(
                "Position en cours de mise à jour",
                "Le technicien doit partager sa position avant que vous puissiez suivre son trajet.",
                "OK");
            return;
        }

        await NavigateProviderTrackingAsync(
            currentMissionId,
            provider.FullName,
            provider.CurrentLatitude.Value,
            provider.CurrentLongitude.Value,
            provider.DestinationLatitude.Value,
            provider.DestinationLongitude.Value,
            provider.EstimatedArrivalMinutes,
            provider.DistanceKm);
    }

    private static async Task NavigateProviderTrackingAsync(
        Guid missionId,
        string providerName,
        decimal providerLatitude,
        decimal providerLongitude,
        decimal destinationLatitude,
        decimal destinationLongitude,
        int? estimatedArrivalMinutes,
        decimal? distanceKm)
    {
        var route = $"{nameof(ProviderTrackingPage)}" +
            $"?missionId={missionId:D}" +
            $"&providerName={Uri.EscapeDataString(providerName)}" +
            $"&providerLat={FormatCoordinate(providerLatitude)}" +
            $"&providerLon={FormatCoordinate(providerLongitude)}" +
            $"&destinationLat={FormatCoordinate(destinationLatitude)}" +
            $"&destinationLon={FormatCoordinate(destinationLongitude)}" +
            $"&eta={estimatedArrivalMinutes?.ToString(CultureInfo.InvariantCulture) ?? string.Empty}" +
            $"&distance={distanceKm?.ToString("0.0", CultureInfo.InvariantCulture) ?? string.Empty}";
        await Shell.Current.GoToAsync(route);
    }

    private static string FormatCoordinate(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        if (currentMissionId != Guid.Empty)
        {
            await Shell.Current.GoToAsync($"{nameof(PaymentCheckoutPage)}?missionId={currentMissionId:D}");
        }
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
        if (currentMissionId != Guid.Empty)
        {
            await Shell.Current.GoToAsync($"{nameof(MissionCompletionPage)}?missionId={currentMissionId:D}");
        }
    }

    private async void OnOpenChatClicked(object sender, EventArgs e)
    {
        if (currentMissionId == Guid.Empty || isOpeningChat)
        {
            return;
        }

        isOpeningChat = true;
        try
        {
            await Shell.Current.GoToAsync($"{nameof(MissionChatPage)}?missionId={currentMissionId:D}");
        }
        catch
        {
            isOpeningChat = false;
            ShowError("La conversation ne peut pas être ouverte pour le moment.");
        }
    }

    private static bool IsConversationActive(string status)
        => status.Equals("Accepted", StringComparison.OrdinalIgnoreCase)
            || status.Equals("OnTheWay", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Started", StringComparison.OrdinalIgnoreCase);

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
        var companyReviewStarted = mission.AssignedCompany is not null;
        var providerAssigned = mission.AssignedProvider is not null;
        var providerAccepted = mission.ProviderAcceptedAt.HasValue;
        var confirmed = mission.CustomerConfirmedAt.HasValue || mission.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase);
        var started = status is "started" or "ontheway" or "completed";
        var completed = status is "completed";

        return
        [
            TimelineRow.Done("Demande envoyée", mission.CreatedAt.ToString("dd/MM HH:mm")),
            companyReviewStarted
                ? TimelineRow.Done("Intervention en préparation", mission.AssignedCompany!.Name)
                : TimelineRow.Pending("Recherche d'une entreprise", "Votre demande est proposée aux entreprises disponibles."),
            providerAccepted
                ? TimelineRow.Done("Votre prestataire est prêt", mission.AssignedProvider!.FullName)
                : providerAssigned
                    ? TimelineRow.Pending("Confirmation du prestataire", "Le prestataire confirme sa disponibilité.")
                    : TimelineRow.Pending("Prestataire à attribuer", "L'entreprise prépare l'intervention."),
            confirmed
                ? TimelineRow.Done("Paiement confirmé", "La mission peut démarrer.")
                : TimelineRow.Pending("Paiement client", "Disponible dès que le prestataire est prêt."),
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
            "Quoted" => "Intervention en préparation",
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
