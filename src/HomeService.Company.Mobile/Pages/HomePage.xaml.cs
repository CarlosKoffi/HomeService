using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HomeService.Company.Mobile.Services;
using HomeService.Contracts.CompanyPortal;
using HomeService.Mobile.Shared;

namespace HomeService.Company.Mobile.Pages;

public partial class HomePage : ContentPage
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(15);
    private readonly CompanyMobileApiClient apiClient;
    private readonly CompanySessionStore sessionStore;
    private readonly CatalogMediaResolver catalogMedia;
    private readonly ObservableCollection<OfferRow> offers = [];
    private CancellationTokenSource? refreshCancellation;
    private bool loading;

    public HomePage()
    {
        InitializeComponent();
        apiClient = IPlatformApplication.Current!.Services.GetRequiredService<CompanyMobileApiClient>();
        sessionStore = IPlatformApplication.Current.Services.GetRequiredService<CompanySessionStore>();
        catalogMedia = IPlatformApplication.Current.Services.GetRequiredService<CatalogMediaResolver>();
        OffersView.ItemsSource = offers;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
        StartRefresh();
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
        if (loading) return;
        loading = true;
        try
        {
            var token = await sessionStore.GetTokenAsync();
            var companyId = await sessionStore.GetCompanyIdAsync();
            if (string.IsNullOrWhiteSpace(token) || !companyId.HasValue)
            {
                App.ShowLogin();
                return;
            }

            var companyName = await sessionStore.GetCompanyNameAsync();
            var userName = await sessionStore.GetUserNameAsync();
            GreetingLabel.Text = $"Bonjour {FirstName(userName)} 👋";
            CompanyLabel.Text = companyName ?? "Votre entreprise";

            var offersTask = apiClient.GetOffersAsync(token, companyId.Value);
            var missionsTask = apiClient.GetMissionsAsync(token, companyId.Value);
            var badgesTask = apiClient.GetNavigationBadgesAsync(token, companyId.Value);
            await Task.WhenAll(offersTask, missionsTask, badgesTask);
            var offerResult = await offersTask;
            var missionResult = await missionsTask;
            var badgeResult = await badgesTask;
            if (!offerResult.IsSuccess || !missionResult.IsSuccess)
            {
                if (!quiet) ShowError(offerResult.ErrorMessage ?? missionResult.ErrorMessage ?? "Actualisation impossible.");
                return;
            }

            var activeOffers = (offerResult.Response ?? [])
                .Where(item => item.CanAccept || item.CanRefuse)
                .OrderBy(item => item.ExpiresAt)
                .ToList();
            var offerRows = await Task.WhenAll(activeOffers.Select(async offer =>
                new OfferRow(offer, await catalogMedia.ResolveServiceAsync(null, offer.ServiceName))));
            offers.Clear();
            foreach (var row in offerRows)
            {
                offers.Add(row);
            }

            var missions = missionResult.Response ?? [];
            OffersCountLabel.Text = offers.Count.ToString();
            AssignCountLabel.Text = missions.Count(IsWaitingForAssignment).ToString();
            ActiveCountLabel.Text = missions.Count(IsActive).ToString();
            var alertCount = badgeResult.Response?.AlertCount ?? 0;
            AlertBadge.IsVisible = alertCount > 0;
            AlertBadgeLabel.Text = alertCount > 99 ? "99+" : alertCount.ToString();
            EmptyOffersLabel.IsVisible = offers.Count == 0;
            ErrorLabel.IsVisible = false;
            if (Shell.Current is AppShell shell) _ = shell.RefreshNavigationBadgesAsync();
        }
        finally
        {
            loading = false;
        }
    }

    private void StartRefresh()
    {
        refreshCancellation?.Cancel();
        refreshCancellation?.Dispose();
        refreshCancellation = new CancellationTokenSource();
        _ = RefreshLoopAsync(refreshCancellation.Token);
    }

    private async Task RefreshLoopAsync(CancellationToken cancellationToken)
    {
        var second = 0;
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                foreach (var offer in offers) offer.RefreshCountdown();
                second++;
                if (second % (int)RefreshInterval.TotalSeconds == 0)
                {
                    await LoadAsync(quiet: true);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadAsync();
        RefreshHost.IsRefreshing = false;
    }

    private async void OnAcceptOfferClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not OfferRow row || row.Offer.OfferId is null) return;
        row.DisableActions();
        var token = await sessionStore.GetTokenAsync();
        var companyId = await sessionStore.GetCompanyIdAsync();
        if (string.IsNullOrWhiteSpace(token) || !companyId.HasValue) return;
        var result = await apiClient.AcceptOfferAsync(token, companyId.Value, row.Offer.OfferId.Value);
        if (!result.IsSuccess)
        {
            row.RestoreActions();
            ShowError(result.ErrorMessage ?? "Cette mission ne peut plus être acceptée.");
            return;
        }

        await LoadAsync();
        await Shell.Current.GoToAsync($"{nameof(MissionDetailPage)}?missionId={row.Offer.MissionId:D}");
    }

    private async void OnRefuseOfferClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not OfferRow row || row.Offer.OfferId is null) return;
        var confirmed = await DisplayAlert(
            "Refuser cette mission ?",
            "Elle ne sera plus disponible pour votre entreprise.",
            "Refuser",
            "Annuler");
        if (!confirmed) return;

        row.DisableActions();
        var token = await sessionStore.GetTokenAsync();
        var companyId = await sessionStore.GetCompanyIdAsync();
        if (string.IsNullOrWhiteSpace(token) || !companyId.HasValue) return;
        var result = await apiClient.RefuseOfferAsync(token, companyId.Value, row.Offer.OfferId.Value);
        if (!result.IsSuccess)
        {
            row.RestoreActions();
            ShowError(result.ErrorMessage ?? "Cette mission ne peut plus être refusée.");
            return;
        }

        await LoadAsync();
    }

    private async void OnOfferDetailsClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is OfferRow row)
        {
            await Shell.Current.GoToAsync($"{nameof(MissionDetailPage)}?missionId={row.Offer.MissionId:D}");
        }
    }

    private async void OnNotificationsClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync("//notifications");

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }

    private static bool IsWaitingForAssignment(CompanyPortalMissionResponse mission)
        => mission.ProviderId is null && mission.Status is "SearchingProvider" or "Assigned" or "Offered";

    private static bool IsActive(CompanyPortalMissionResponse mission)
        => mission.Status is "Accepted" or "OnTheWay" or "Started";

    private static string FirstName(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];

    public sealed class OfferRow : INotifyPropertyChanged
    {
        private bool canAccept;
        private bool canRefuse;
        private string remainingText = string.Empty;

        public OfferRow(CompanyMissionOfferResponse offer, ImageSource? serviceImage)
        {
            Offer = offer;
            ServiceImage = serviceImage ?? "icon_mission.svg";
            canAccept = offer.CanAccept;
            canRefuse = offer.CanRefuse;
            RefreshCountdown();
        }

        public CompanyMissionOfferResponse Offer { get; }
        public ImageSource ServiceImage { get; }
        public string ServiceName => Offer.ServiceName;
        public string CustomerAndLocation => $"{Offer.CustomerName}\n{Offer.ServiceAddress ?? "Adresse à confirmer"}";
        public string ScheduleLabel => Offer.ScheduledFor?.ToLocalTime().ToString("dd/MM/yyyy · HH:mm") ?? "Dès que possible";
        public string RemainingText { get => remainingText; private set => SetField(ref remainingText, value); }
        public bool CanAccept { get => canAccept; set => SetField(ref canAccept, value); }
        public bool CanRefuse { get => canRefuse; set => SetField(ref canRefuse, value); }

        public void DisableActions()
        {
            CanAccept = false;
            CanRefuse = false;
        }

        public void RestoreActions()
        {
            CanAccept = Offer.CanAccept;
            CanRefuse = Offer.CanRefuse;
        }

        public void RefreshCountdown()
        {
            var remaining = Offer.ExpiresAt - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                RemainingText = "Expirée";
                CanAccept = false;
                CanRefuse = false;
                return;
            }

            RemainingText = remaining.TotalHours >= 1
                ? $"{(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}"
                : $"{remaining.Minutes:00}:{remaining.Seconds:00}";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
