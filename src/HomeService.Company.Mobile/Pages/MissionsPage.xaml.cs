using System.Collections.ObjectModel;
using HomeService.Company.Mobile.Services;
using HomeService.Contracts.CompanyPortal;
using HomeService.Mobile.Shared;

namespace HomeService.Company.Mobile.Pages;

public partial class MissionsPage : ContentPage
{
    private readonly CompanyMobileApiClient apiClient;
    private readonly CompanySessionStore sessionStore;
    private readonly CatalogMediaResolver catalogMedia;
    private readonly ObservableCollection<MissionRow> visibleMissions = [];
    private IReadOnlyList<CompanyPortalMissionResponse> allMissions = [];
    private CancellationTokenSource? refreshCancellation;
    private bool loading;

    public MissionsPage()
    {
        InitializeComponent();
        apiClient = IPlatformApplication.Current!.Services.GetRequiredService<CompanyMobileApiClient>();
        sessionStore = IPlatformApplication.Current.Services.GetRequiredService<CompanySessionStore>();
        catalogMedia = IPlatformApplication.Current.Services.GetRequiredService<CatalogMediaResolver>();
        MissionsView.ItemsSource = visibleMissions;
        FilterPicker.SelectedIndex = 0;
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

    private async Task LoadAsync()
    {
        if (loading) return;
        loading = true;
        try
        {
            var token = await sessionStore.GetTokenAsync();
            var companyId = await sessionStore.GetCompanyIdAsync();
            if (string.IsNullOrWhiteSpace(token) || !companyId.HasValue) return;
            var result = await apiClient.GetMissionsAsync(token, companyId.Value);
            if (result.IsSuccess) allMissions = result.Response ?? [];
            await ApplyFilterAsync();
        }
        finally
        {
            loading = false;
        }
    }

    private async Task ApplyFilterAsync()
    {
        var filter = FilterPicker.SelectedItem?.ToString() ?? "Toutes";
        var query = allMissions.Where(mission => filter switch
        {
            "À affecter" => mission.ProviderId is null && mission.Status is "SearchingProvider" or "Assigned" or "Offered",
            "En cours" => mission.Status is "Accepted" or "OnTheWay" or "Started",
            "Terminées" => mission.Status == "Completed",
            "Annulées" => mission.Status == "Cancelled",
            _ => true
        });

        var ordered = query.OrderBy(MissionSort).ThenBy(item => item.ScheduledFor ?? DateTimeOffset.MaxValue).ToList();
        var rows = await Task.WhenAll(ordered.Select(async mission => MissionRow.From(
            mission,
            await catalogMedia.ResolveServiceAsync(null, mission.ServiceName))));
        visibleMissions.Clear();
        foreach (var row in rows)
        {
            visibleMissions.Add(row);
        }
    }

    private async Task RefreshLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await MainThread.InvokeOnMainThreadAsync(LoadAsync);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private async void OnFilterChanged(object? sender, EventArgs e) => await ApplyFilterAsync();

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadAsync();
        RefreshHost.IsRefreshing = false;
    }

    private async void OnMissionSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not MissionRow row) return;
        MissionsView.SelectedItem = null;
        await Shell.Current.GoToAsync($"{nameof(MissionDetailPage)}?missionId={row.Mission.Id:D}");
    }

    private static int MissionSort(CompanyPortalMissionResponse mission)
        => mission.ProviderId is null ? 0 : mission.Status is "Accepted" or "OnTheWay" or "Started" ? 1 : 2;

    public sealed record MissionRow(
        CompanyPortalMissionResponse Mission,
        ImageSource ServiceImage,
        string ServiceName,
        string CustomerLabel,
        string ProviderLabel,
        string ScheduleLabel,
        string StatusLabel,
        Color StatusColor)
    {
        public static MissionRow From(CompanyPortalMissionResponse mission, ImageSource? serviceImage)
        {
            var status = mission.Status switch
            {
                "SearchingProvider" or "Assigned" or "Offered" when mission.ProviderId is null => "À affecter",
                "Accepted" => "Confirmée",
                "OnTheWay" => "En route",
                "Started" => "En cours",
                "Completed" => "Terminée",
                "Cancelled" => "Annulée",
                _ => "À suivre"
            };
            var color = status switch
            {
                "À affecter" => Color.FromArgb("#F59E0B"),
                "En route" or "En cours" => Color.FromArgb("#155EEF"),
                "Terminée" => Color.FromArgb("#16B364"),
                "Annulée" => Color.FromArgb("#DC2626"),
                _ => Color.FromArgb("#667085")
            };
            return new MissionRow(
                mission,
                serviceImage ?? "icon_mission.svg",
                mission.ServiceName,
                $"{mission.CustomerName} · {mission.LocationLabel ?? "Adresse à confirmer"}",
                mission.ProviderName ?? "Prestataire à affecter",
                mission.ScheduledFor?.ToLocalTime().ToString("dd/MM/yyyy · HH:mm") ?? "Dès que possible",
                status,
                color);
        }
    }
}
