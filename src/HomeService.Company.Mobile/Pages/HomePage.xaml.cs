using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HomeService.Company.Mobile.Services;
using HomeService.Contracts.CompanyPortal;

namespace HomeService.Company.Mobile.Pages;

public partial class HomePage : ContentPage
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(15);
    private readonly CompanyMobileApiClient apiClient;
    private readonly CompanySessionStore sessionStore;
    private readonly ObservableCollection<OfferRow> offers = [];
    private CancellationTokenSource? refreshCancellation;
    private bool loading;

    public HomePage()
    {
        InitializeComponent();
        apiClient = IPlatformApplication.Current!.Services.GetRequiredService<CompanyMobileApiClient>();
        sessionStore = IPlatformApplication.Current.Services.GetRequiredService<CompanySessionStore>();
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
            await Task.WhenAll(offersTask, missionsTask);
            var offerResult = await offersTask;
            var missionResult = await missionsTask;
            if (!offerResult.IsSuccess || !missionResult.IsSuccess)
            {
                if (!quiet) ShowError(offerResult.ErrorMessage ?? missionResult.ErrorMessage ?? "Actualisation impossible.");
                return;
            }

            offers.Clear();
            foreach (var offer in (offerResult.Response ?? []).Where(item => item.CanAccept).OrderBy(item => item.ExpiresAt))
            {
                offers.Add(new OfferRow(offer));
            }

            var missions = missionResult.Response ?? [];
            OffersCountLabel.Text = offers.Count.ToString();
            AssignCountLabel.Text = missions.Count(IsWaitingForAssignment).ToString();
            ActiveCountLabel.Text = missions.Count(IsActive).ToString();
            EmptyOffersLabel.IsVisible = offers.Count == 0;
            ErrorLabel.IsVisible = false;
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
        row.CanAccept = false;
        var token = await sessionStore.GetTokenAsync();
        var companyId = await sessionStore.GetCompanyIdAsync();
        if (string.IsNullOrWhiteSpace(token) || !companyId.HasValue) return;
        var result = await apiClient.AcceptOfferAsync(token, companyId.Value, row.Offer.OfferId.Value);
        if (!result.IsSuccess)
        {
            row.CanAccept = true;
            ShowError(result.ErrorMessage ?? "Cette mission ne peut plus être acceptée.");
            return;
        }

        await LoadAsync();
        await Shell.Current.GoToAsync($"{nameof(MissionDetailPage)}?missionId={row.Offer.MissionId:D}");
    }

    private async void OnOfferDetailsClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is OfferRow row)
        {
            await DisplayAlert(row.Offer.ServiceName, $"{row.CustomerAndLocation}\n{row.Offer.Description}", "Fermer");
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
        private string remainingText = string.Empty;

        public OfferRow(CompanyMissionOfferResponse offer)
        {
            Offer = offer;
            canAccept = offer.CanAccept;
            RefreshCountdown();
        }

        public CompanyMissionOfferResponse Offer { get; }
        public string ServiceName => Offer.ServiceName;
        public string CustomerAndLocation => $"{Offer.CustomerName}\n{Offer.ServiceAddress ?? "Adresse à confirmer"}";
        public string ScheduleLabel => Offer.ScheduledFor?.ToLocalTime().ToString("dd/MM/yyyy · HH:mm") ?? "Dès que possible";
        public string RemainingText { get => remainingText; private set => SetField(ref remainingText, value); }
        public bool CanAccept { get => canAccept; set => SetField(ref canAccept, value); }

        public void RefreshCountdown()
        {
            var remaining = Offer.ExpiresAt - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                RemainingText = "Expirée";
                CanAccept = false;
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
