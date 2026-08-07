using System.Collections.ObjectModel;
using HomeService.Company.Mobile.Services;
using HomeService.Contracts.CompanyPortal;
using HomeService.Contracts.Missions;
using HomeService.Mobile.Shared;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;

namespace HomeService.Company.Mobile.Pages;

[QueryProperty(nameof(MissionId), "missionId")]
public partial class MissionDetailPage : ContentPage
{
    private readonly CompanyMobileApiClient apiClient;
    private readonly CompanySessionStore sessionStore;
    private readonly CatalogMediaResolver catalogMedia;
    private readonly ObservableCollection<AdditionalQuoteRow> additionalQuotes = [];
    private CompanyPortalMissionDetailResponse? detail;
    private CompanyPortalMissionResponse? mission;
    private IReadOnlyList<CompanyPortalAssignableProviderResponse> candidates = [];
    private MissionAdditionalQuoteResponse? selectedAdditionalQuote;
    private CancellationTokenSource? refreshCancellation;
    private Guid missionId;
    private bool loading;

    public string? MissionId
    {
        set { if (Guid.TryParse(value, out var parsed)) missionId = parsed; }
    }

    public MissionDetailPage()
    {
        InitializeComponent();
        apiClient = IPlatformApplication.Current!.Services.GetRequiredService<CompanyMobileApiClient>();
        sessionStore = IPlatformApplication.Current.Services.GetRequiredService<CompanySessionStore>();
        catalogMedia = IPlatformApplication.Current.Services.GetRequiredService<CatalogMediaResolver>();
        AdditionalQuotesView.ItemsSource = additionalQuotes;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
        refreshCancellation?.Cancel();
        refreshCancellation?.Dispose();
        refreshCancellation = new CancellationTokenSource();
        _ = RefreshLoopAsync(refreshCancellation.Token);
    }

    protected override void OnDisappearing()
    {
        refreshCancellation?.Cancel();
        refreshCancellation?.Dispose();
        refreshCancellation = null;
        base.OnDisappearing();
    }

    private async Task LoadAsync(bool quiet = false)
    {
        if (loading || missionId == Guid.Empty) return;
        loading = true;
        try
        {
            var token = await sessionStore.GetTokenAsync();
            var companyId = await sessionStore.GetCompanyIdAsync();
            if (string.IsNullOrWhiteSpace(token) || !companyId.HasValue) return;

            var detailResult = await apiClient.GetMissionDetailAsync(token, companyId.Value, missionId);
            if (!detailResult.IsSuccess || detailResult.Response is null)
            {
                if (!quiet) ShowMessage(detailResult.ErrorMessage ?? "Mission introuvable.", true);
                return;
            }

            detail = detailResult.Response;
            mission = detail.Mission;

            var candidatesTask = detail.CanAssign
                ? apiClient.GetAssignableProvidersAsync(token, companyId.Value, missionId)
                : Task.FromResult(ApiCallResult<IReadOnlyList<CompanyPortalAssignableProviderResponse>>.Ok([]));
            var quotesTask = detail.CanAccept || detail.CanRefuse
                ? Task.FromResult(ApiCallResult<IReadOnlyList<MissionAdditionalQuoteResponse>>.Ok([]))
                : apiClient.GetAdditionalQuotesAsync(token, companyId.Value, missionId);
            await Task.WhenAll(candidatesTask, quotesTask);

            candidates = (await candidatesTask).Response ?? [];
            await RenderMissionAsync(detail);
            RenderCandidates();
            RenderAdditionalQuotes((await quotesTask).Response ?? []);
            RenderHistory(detail.CustomerHistory);
            MessageLabel.IsVisible = false;
        }
        finally
        {
            loading = false;
        }
    }

    private async Task RenderMissionAsync(CompanyPortalMissionDetailResponse response)
    {
        var item = response.Mission;
        MissionNumberLabel.Text = item.MissionNumber;
        StatusLabel.Text = ResolveStatus(response);
        ServiceLabel.Text = item.ServiceName;
        PrestationLabel.Text = string.Join(" · ", new[] { response.PrestationName, response.OptionName }.Where(value => !string.IsNullOrWhiteSpace(value)));
        PrestationLabel.IsVisible = !string.IsNullOrWhiteSpace(PrestationLabel.Text);
        var serviceMedia = string.IsNullOrWhiteSpace(response.PrestationName)
            ? await catalogMedia.ResolveServiceAsync(null, item.ServiceName)
            : await catalogMedia.ResolvePrestationAsync(null, response.PrestationName, serviceName: item.ServiceName);
        ServiceImage.Source = serviceMedia ?? "icon_mission.svg";
        ScheduleLabel.Text = item.ScheduledFor?.ToLocalTime().ToString("dd/MM/yyyy · HH:mm") ?? "Dès que possible";
        AddressLabel.Text = item.LocationLabel ?? "Adresse à confirmer";
        DescriptionLabel.Text = response.Description;
        DescriptionLabel.IsVisible = !string.IsNullOrWhiteSpace(response.Description);
        PriceLabel.Text = item.CompanyQuotedAmount.HasValue
            ? $"Prix envoyé : {item.CompanyQuotedAmount:N0} {item.Currency}"
            : "Prix à définir après acceptation";

        CustomerLabel.Text = item.CustomerName;
        CustomerPhoneLabel.Text = item.CustomerPhoneNumber;

        OfferActionCard.IsVisible = response.CanAccept || response.CanRefuse;
        AcceptOfferButton.IsVisible = response.CanAccept;
        RefuseOfferButton.IsVisible = response.CanRefuse;
        MissionChatButton.IsEnabled = !OfferActionCard.IsVisible;
        UpdateOfferDeadline(response.OfferExpiresAt);

        AssignmentCard.IsVisible = response.CanAssign;
        ProviderCard.IsVisible = item.ProviderId.HasValue;
        ProviderNameLabel.Text = item.ProviderName ?? "Prestataire";
        ProviderStateLabel.Text = item.Status switch
        {
            "OnTheWay" => "En route vers le client",
            "Started" => "Intervention en cours",
            "Completed" => "Mission terminée",
            _ => "Mission affectée"
        };
        ProviderCallButton.IsEnabled = !string.IsNullOrWhiteSpace(response.ProviderPhoneNumber);
        ProviderMessageButton.IsEnabled = !OfferActionCard.IsVisible;

        DeadlineCard.IsVisible = response.CanAssign && item.CompanyAssignmentExpiresAt.HasValue;
        UpdateDeadline(item.CompanyAssignmentExpiresAt);
        RenderCancellation(item);
        RenderMap(response);
    }

    private void RenderCancellation(CompanyPortalMissionResponse item)
    {
        CancellationCard.IsVisible = string.Equals(item.Status, "Cancelled", StringComparison.OrdinalIgnoreCase);
        if (!CancellationCard.IsVisible)
        {
            return;
        }

        CancellationTitleLabel.Text = item.CancellationActor switch
        {
            "Customer" => "Mission annulée par le client",
            "Provider" => "Mission annulée par le prestataire",
            "Company" => "Mission annulée par votre entreprise",
            "Admin" => "Mission annulée par l’administration",
            _ => "Mission annulée"
        };
        var reason = item.CancellationReason switch
        {
            "CustomerChangedMind" => "Le client n’a plus besoin de l’intervention.",
            "CustomerUnavailable" => "Le client est indisponible.",
            "CustomerAbsent" => "Le client était absent.",
            "AccessRefused" => "L’accès au lieu d’intervention est impossible.",
            "ProviderUnavailable" => "Le prestataire est indisponible.",
            "ProviderNoShow" => "Le prestataire ne s’est pas présenté.",
            "CompanyUnavailable" => "L’entreprise est indisponible.",
            _ => "Autre motif d’annulation."
        };
        CancellationReasonLabel.Text = string.IsNullOrWhiteSpace(item.CancellationComment)
            ? reason
            : $"{reason} Détail : {item.CancellationComment}";
    }

    private void RenderMap(CompanyPortalMissionDetailResponse response)
    {
        var item = response.Mission;
        if (!item.ServiceLatitude.HasValue || !item.ServiceLongitude.HasValue)
        {
            MapCard.IsVisible = false;
            return;
        }

        var clientLocation = new Location((double)item.ServiceLatitude.Value, (double)item.ServiceLongitude.Value);
        MissionMap.Pins.Clear();
        MissionMap.MapElements.Clear();
        MissionMap.Pins.Add(new Pin
        {
            Label = $"Client · {item.CustomerName}",
            Address = item.LocationLabel ?? string.Empty,
            Location = clientLocation,
            Type = PinType.Place
        });

        var center = clientLocation;
        var radius = 0.8;
        if (response.ProviderLatitude.HasValue && response.ProviderLongitude.HasValue)
        {
            var providerLocation = new Location((double)response.ProviderLatitude.Value, (double)response.ProviderLongitude.Value);
            MissionMap.Pins.Add(new Pin
            {
                Label = response.Mission.ProviderName ?? "Prestataire",
                Address = "Position actuelle",
                Location = providerLocation,
                Type = PinType.SavedPin
            });

            var route = new Polyline { StrokeColor = Color.FromArgb("#1A73E8"), StrokeWidth = 5 };
            route.Geopath.Add(providerLocation);
            route.Geopath.Add(clientLocation);
            MissionMap.MapElements.Add(route);

            center = new Location(
                (providerLocation.Latitude + clientLocation.Latitude) / 2,
                (providerLocation.Longitude + clientLocation.Longitude) / 2);
            radius = Math.Max(0.8, (response.ProviderDistanceKilometers ?? 1) * 0.7);
            DistanceLabel.Text = response.ProviderDistanceKilometers.HasValue
                ? $"Prestataire à {response.ProviderDistanceKilometers.Value:0.0} km"
                : "Prestataire localisé";
            DistanceLabel.IsVisible = true;
        }
        else
        {
            DistanceLabel.IsVisible = false;
        }

        MissionMap.MoveToRegion(MapSpan.FromCenterAndRadius(center, Distance.FromKilometers(radius)));
        MapCard.IsVisible = true;
    }

    private void RenderCandidates()
    {
        ProviderPicker.ItemsSource = candidates.Where(item => item.CanAssign).ToList();
        if (ProviderPicker.ItemsSource is IReadOnlyCollection<CompanyPortalAssignableProviderResponse> items && items.Count == 1)
        {
            ProviderPicker.SelectedIndex = 0;
        }
    }

    private void RenderHistory(IReadOnlyList<CompanyCustomerMissionHistoryResponse> rows)
    {
        HistoryList.Children.Clear();
        EmptyHistoryLabel.IsVisible = rows.Count == 0;
        foreach (var row in rows)
        {
            var title = new Label { Text = row.ServiceName, FontAttributes = FontAttributes.Bold, FontSize = 15 };
            var prestation = new Label
            {
                Text = row.PrestationName,
                FontSize = 13,
                TextColor = Color.FromArgb("#667085"),
                IsVisible = !string.IsNullOrWhiteSpace(row.PrestationName)
            };
            var meta = new Label
            {
                Text = $"{row.Date.ToLocalTime():dd/MM/yyyy} · {ResolveHistoryStatus(row.Status)}" + (row.Rating.HasValue ? $" · {row.Rating}/5 ★" : string.Empty),
                FontSize = 13,
                TextColor = Color.FromArgb("#667085")
            };
            HistoryList.Children.Add(new Border
            {
                BackgroundColor = Colors.White,
                Stroke = Color.FromArgb("#E4E7EC"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
                Padding = 14,
                Content = new VerticalStackLayout { Spacing = 4, Children = { title, prestation, meta } }
            });
        }
    }

    private void RenderAdditionalQuotes(IReadOnlyList<MissionAdditionalQuoteResponse> rows)
    {
        additionalQuotes.Clear();
        foreach (var row in rows) additionalQuotes.Add(AdditionalQuoteRow.From(row));
        AdditionalQuoteCard.IsVisible = additionalQuotes.Count > 0;
        var pending = additionalQuotes.FirstOrDefault(item => item.Quote.Status == "Requested");
        if (pending is not null)
        {
            AdditionalQuotesView.SelectedItem = pending;
            SelectAdditionalQuote(pending);
        }
    }

    private async Task RefreshLoopAsync(CancellationToken cancellationToken)
    {
        var second = 0;
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                second++;
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    UpdateDeadline(mission?.CompanyAssignmentExpiresAt);
                    UpdateOfferDeadline(detail?.OfferExpiresAt);
                });
                if (second % 15 == 0)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => LoadAsync(quiet: true));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private void UpdateOfferDeadline(DateTimeOffset? expiresAt)
    {
        if (!expiresAt.HasValue || !OfferActionCard.IsVisible) return;
        var remaining = expiresAt.Value - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            OfferDeadlineLabel.Text = "Délai expiré";
            AcceptOfferButton.IsEnabled = false;
            RefuseOfferButton.IsEnabled = false;
            return;
        }

        OfferDeadlineLabel.Text = $"Temps restant : {(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";
        AcceptOfferButton.IsEnabled = detail?.CanAccept == true;
        RefuseOfferButton.IsEnabled = detail?.CanRefuse == true;
    }

    private void UpdateDeadline(DateTimeOffset? expiresAt)
    {
        if (!expiresAt.HasValue || !AssignmentCard.IsVisible)
        {
            DeadlineCard.IsVisible = false;
            return;
        }

        var remaining = expiresAt.Value - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            DeadlineLabel.Text = "Délai expiré · redistribution en cours";
            AssignButton.IsEnabled = false;
            return;
        }

        DeadlineLabel.Text = $"Temps restant : {(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";
        AssignButton.IsEnabled = true;
    }

    private void OnProviderSelected(object? sender, EventArgs e)
    {
        if (ProviderPicker.SelectedItem is not CompanyPortalAssignableProviderResponse provider) return;
        var minimum = provider.PriceMinAmount ?? provider.NormalPriceAmount;
        var maximum = provider.PriceMaxAmount ?? provider.PremiumPriceAmount;
        QuoteAmountEntry.Text = minimum.ToString();
        PriceRangeLabel.Text = provider.IsFixedPrice
            ? $"Prix fixe : {maximum:N0} {provider.Currency}"
            : $"Fourchette : {minimum:N0} – {maximum:N0} {provider.Currency}";
    }

    private async void OnAcceptOfferClicked(object? sender, EventArgs e)
    {
        if (detail?.OfferId is not Guid offerId) return;
        SetOfferButtonsEnabled(false);
        var token = await sessionStore.GetTokenAsync();
        var companyId = await sessionStore.GetCompanyIdAsync();
        if (string.IsNullOrWhiteSpace(token) || !companyId.HasValue) return;
        var result = await apiClient.AcceptOfferAsync(token, companyId.Value, offerId);
        ShowMessage(result.IsSuccess ? "Mission acceptée. Vous pouvez maintenant l’affecter." : result.ErrorMessage ?? "Acceptation impossible.", !result.IsSuccess);
        await LoadAsync();
    }

    private async void OnRefuseOfferClicked(object? sender, EventArgs e)
    {
        if (detail?.OfferId is not Guid offerId) return;
        var confirmed = await DisplayAlert("Refuser cette mission ?", "Elle ne sera plus proposée à votre entreprise.", "Refuser", "Annuler");
        if (!confirmed) return;
        SetOfferButtonsEnabled(false);
        var token = await sessionStore.GetTokenAsync();
        var companyId = await sessionStore.GetCompanyIdAsync();
        if (string.IsNullOrWhiteSpace(token) || !companyId.HasValue) return;
        var result = await apiClient.RefuseOfferAsync(token, companyId.Value, offerId);
        if (result.IsSuccess) await Shell.Current.GoToAsync("..");
        else
        {
            ShowMessage(result.ErrorMessage ?? "Refus impossible.", true);
            SetOfferButtonsEnabled(true);
        }
    }

    private void SetOfferButtonsEnabled(bool enabled)
    {
        AcceptOfferButton.IsEnabled = enabled && detail?.CanAccept == true;
        RefuseOfferButton.IsEnabled = enabled && detail?.CanRefuse == true;
    }

    private async void OnAssignClicked(object? sender, EventArgs e)
    {
        if (mission is null || ProviderPicker.SelectedItem is not CompanyPortalAssignableProviderResponse provider
            || !int.TryParse(QuoteAmountEntry.Text, out var amount) || amount <= 0)
        {
            ShowMessage("Choisissez un prestataire et un montant valide.", true);
            return;
        }

        var token = await sessionStore.GetTokenAsync();
        var companyId = await sessionStore.GetCompanyIdAsync();
        if (string.IsNullOrWhiteSpace(token) || !companyId.HasValue) return;
        AssignButton.IsEnabled = false;
        var result = await apiClient.AssignMissionAsync(
            token,
            companyId.Value,
            mission.Id,
            new AssignCompanyMissionRequest(provider.Id, amount, null));
        ShowMessage(result.IsSuccess ? "Mission affectée. Le prestataire doit maintenant accepter." : result.ErrorMessage ?? "Affectation impossible.", !result.IsSuccess);
        await LoadAsync();
    }

    private void OnAdditionalQuoteSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is AdditionalQuoteRow row) SelectAdditionalQuote(row);
    }

    private void SelectAdditionalQuote(AdditionalQuoteRow row)
    {
        selectedAdditionalQuote = row.Quote;
        ProviderRemarkLabel.Text = row.Quote.Reason;
        QuoteEditor.IsVisible = row.Quote.Status == "Requested";
        QuoteReadOnlyCard.IsVisible = row.Quote.Status != "Requested";
        QuoteReadOnlyTitle.Text = row.Quote.Status == "Paid"
            ? "Devis payé - mission débloquée"
            : row.Quote.Status == "Submitted"
                ? "Devis envoyé - paiement attendu"
                : $"Devis {row.StatusLabel.ToLowerInvariant()}";
        QuoteReadOnlyDetail.Text = row.Quote.Status == "Submitted"
            ? $"{row.Quote.Amount:N0} {row.Quote.Currency} - {row.Quote.Description}. Ce devis est maintenant en lecture seule."
            : row.Quote.Status == "Paid"
                ? $"{row.Quote.Amount:N0} {row.Quote.Currency} - le prestataire peut reprendre et terminer la mission."
                : row.Quote.Description ?? row.Quote.Reason;
        AdditionalAmountEntry.Text = row.Quote.Amount?.ToString() ?? string.Empty;
        AdditionalDescriptionEditor.Text = row.Quote.Description ?? row.Quote.Reason;
    }

    private async void OnSubmitAdditionalQuoteClicked(object? sender, EventArgs e)
    {
        if (mission is null || selectedAdditionalQuote is null
            || !int.TryParse(AdditionalAmountEntry.Text, out var amount) || amount <= 0
            || string.IsNullOrWhiteSpace(AdditionalDescriptionEditor.Text))
        {
            ShowMessage("Saisissez le montant et le détail du devis.", true);
            return;
        }

        var token = await sessionStore.GetTokenAsync();
        var companyId = await sessionStore.GetCompanyIdAsync();
        if (string.IsNullOrWhiteSpace(token) || !companyId.HasValue) return;
        var result = await apiClient.SubmitAdditionalQuoteAsync(
            token,
            companyId.Value,
            mission.Id,
            selectedAdditionalQuote.Id,
            new SubmitMissionAdditionalQuoteRequest(amount, mission.Currency, AdditionalDescriptionEditor.Text.Trim()));
        ShowMessage(result.IsSuccess ? "Le devis complémentaire a été envoyé au client." : result.ErrorMessage ?? "Envoi impossible.", !result.IsSuccess);
        await LoadAsync();
    }

    private async void OnCustomerCallClicked(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(mission?.CustomerPhoneNumber)) await Launcher.Default.OpenAsync($"tel:{mission.CustomerPhoneNumber}");
    }

    private async void OnProviderCallClicked(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(detail?.ProviderPhoneNumber)) await Launcher.Default.OpenAsync($"tel:{detail.ProviderPhoneNumber}");
    }

    private async void OnMissionChatClicked(object? sender, EventArgs e)
    {
        if (mission is not null) await Shell.Current.GoToAsync($"{nameof(ChatPage)}?missionId={mission.Id:D}");
    }

    private void OnMissionTabClicked(object? sender, EventArgs e)
    {
        MissionContent.IsVisible = true;
        HistoryContent.IsVisible = false;
        MissionTabButton.Style = (Style)Resources["MissionTabActive"];
        HistoryTabButton.Style = (Style)Resources["MissionTabInactive"];
    }

    private void OnHistoryTabClicked(object? sender, EventArgs e)
    {
        MissionContent.IsVisible = false;
        HistoryContent.IsVisible = true;
        MissionTabButton.Style = (Style)Resources["MissionTabInactive"];
        HistoryTabButton.Style = (Style)Resources["MissionTabActive"];
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadAsync();
        RefreshHost.IsRefreshing = false;
    }

    private void ShowMessage(string message, bool error)
    {
        MessageLabel.Text = message;
        MessageLabel.TextColor = error ? Color.FromArgb("#DC2626") : Color.FromArgb("#16B364");
        MessageLabel.IsVisible = true;
    }

    private static string ResolveStatus(CompanyPortalMissionDetailResponse response)
    {
        if (response.CanAccept || response.CanRefuse) return "À VALIDER";
        return response.Mission.Status switch
        {
            "SearchingProvider" or "Assigned" or "Offered" when response.Mission.ProviderId is null => "À AFFECTER",
            "Accepted" => "CONFIRMÉE",
            "OnTheWay" => "EN ROUTE",
            "Started" => "EN COURS",
            "Completed" => "TERMINÉE",
            "Cancelled" => "ANNULÉE",
            _ => response.Mission.Status.ToUpperInvariant()
        };
    }

    private static string ResolveHistoryStatus(string status) => status switch
    {
        "Completed" => "Terminée",
        "Cancelled" => "Annulée",
        "Started" => "En cours",
        _ => status
    };

    public sealed record AdditionalQuoteRow(MissionAdditionalQuoteResponse Quote, string Reason, string StatusLabel)
    {
        public static AdditionalQuoteRow From(MissionAdditionalQuoteResponse quote)
            => new(quote, quote.Reason, quote.Status switch
            {
                "Requested" => "Action entreprise requise",
                "Submitted" => $"Envoyé au client · {quote.Amount:N0} {quote.Currency}",
                "Paid" => "Payé par le client",
                _ => quote.Status
            });
    }
}
