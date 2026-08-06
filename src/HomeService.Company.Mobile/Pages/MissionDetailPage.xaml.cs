using System.Collections.ObjectModel;
using HomeService.Company.Mobile.Services;
using HomeService.Contracts.CompanyPortal;
using HomeService.Contracts.Missions;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;

namespace HomeService.Company.Mobile.Pages;

[QueryProperty(nameof(MissionId), "missionId")]
public partial class MissionDetailPage : ContentPage
{
    private readonly CompanyMobileApiClient apiClient;
    private readonly CompanySessionStore sessionStore;
    private readonly ObservableCollection<AdditionalQuoteRow> additionalQuotes = [];
    private CompanyPortalMissionResponse? mission;
    private IReadOnlyList<CompanyPortalAssignableProviderResponse> candidates = [];
    private CompanyEmployeeResponse? assignedProvider;
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
        AdditionalQuotesView.ItemsSource = additionalQuotes;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
        refreshCancellation?.Cancel();
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

            var missionsResult = await apiClient.GetMissionsAsync(token, companyId.Value);
            mission = missionsResult.Response?.FirstOrDefault(item => item.Id == missionId);
            if (mission is null)
            {
                if (!quiet) ShowMessage("Mission introuvable.", true);
                return;
            }

            var providersTask = apiClient.GetProvidersAsync(token, companyId.Value);
            var quotesTask = apiClient.GetAdditionalQuotesAsync(token, companyId.Value, missionId);
            var candidatesTask = mission.ProviderId is null
                ? apiClient.GetAssignableProvidersAsync(token, companyId.Value, missionId)
                : Task.FromResult(ApiCallResult<IReadOnlyList<CompanyPortalAssignableProviderResponse>>.Ok([]));
            await Task.WhenAll(providersTask, quotesTask, candidatesTask);
            assignedProvider = (await providersTask).Response?.FirstOrDefault(item => item.Id == mission.ProviderId);
            candidates = (await candidatesTask).Response ?? [];
            RenderMission(mission);
            RenderCandidates();
            RenderAdditionalQuotes((await quotesTask).Response ?? []);
        }
        finally
        {
            loading = false;
        }
    }

    private void RenderMission(CompanyPortalMissionResponse item)
    {
        MissionNumberLabel.Text = item.MissionNumber;
        StatusLabel.Text = ResolveStatus(item);
        ServiceLabel.Text = item.ServiceName;
        CustomerLabel.Text = item.CustomerName;
        ScheduleLabel.Text = item.ScheduledFor?.ToLocalTime().ToString("dd/MM/yyyy · HH:mm") ?? "Dès que possible";
        AddressLabel.Text = item.LocationLabel ?? "Adresse à confirmer";
        PriceLabel.Text = item.CompanyQuotedAmount.HasValue
            ? $"Prix envoyé : {item.CompanyQuotedAmount:N0} {item.Currency}"
            : "Prix à définir lors de l’affectation";

        AssignmentCard.IsVisible = item.ProviderId is null && item.Status is not ("Completed" or "Cancelled");
        ProviderCard.IsVisible = item.ProviderId.HasValue;
        ProviderNameLabel.Text = item.ProviderName ?? "Prestataire";
        ProviderStateLabel.Text = item.Status switch
        {
            "OnTheWay" => "En route vers le client",
            "Started" => "Intervention en cours",
            "Completed" => "Mission terminée",
            _ => "Mission affectée"
        };
        ProviderCallButton.IsEnabled = assignedProvider is not null;
        ProviderMessageButton.IsEnabled = assignedProvider is not null;

        DeadlineCard.IsVisible = AssignmentCard.IsVisible && item.CompanyAssignmentExpiresAt.HasValue;
        UpdateDeadline(item.CompanyAssignmentExpiresAt);
        RenderMap(item);
    }

    private void RenderMap(CompanyPortalMissionResponse item)
    {
        if (!item.ServiceLatitude.HasValue || !item.ServiceLongitude.HasValue)
        {
            MapCard.IsVisible = false;
            return;
        }

        var location = new Location((double)item.ServiceLatitude.Value, (double)item.ServiceLongitude.Value);
        MissionMap.Pins.Clear();
        MissionMap.Pins.Add(new Pin { Label = item.ServiceName, Address = item.LocationLabel ?? string.Empty, Location = location });
        MissionMap.MoveToRegion(MapSpan.FromCenterAndRadius(location, Distance.FromKilometers(0.8)));
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
                await MainThread.InvokeOnMainThreadAsync(() => UpdateDeadline(mission?.CompanyAssignmentExpiresAt));
                if (second % 15 == 0)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => LoadAsync(quiet: true));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
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

    private async void OnProviderCallClicked(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(assignedProvider?.PhoneNumber)) await Launcher.Default.OpenAsync($"tel:{assignedProvider.PhoneNumber}");
    }

    private async void OnProviderMessageClicked(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(assignedProvider?.PhoneNumber)) await Launcher.Default.OpenAsync($"sms:{assignedProvider.PhoneNumber}");
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

    private static string ResolveStatus(CompanyPortalMissionResponse item) => item.Status switch
    {
        "SearchingProvider" or "Assigned" or "Offered" when item.ProviderId is null => "À AFFECTER",
        "Accepted" => "CONFIRMÉE",
        "OnTheWay" => "EN ROUTE",
        "Started" => "EN COURS",
        "Completed" => "TERMINÉE",
        "Cancelled" => "ANNULÉE",
        _ => item.Status.ToUpperInvariant()
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
