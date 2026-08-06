using System.Collections.ObjectModel;
using HomeService.Company.Mobile.Services;
using HomeService.Contracts.CompanyPortal;

namespace HomeService.Company.Mobile.Pages;

public partial class ProvidersPage : ContentPage
{
    private readonly CompanyMobileApiClient apiClient;
    private readonly CompanySessionStore sessionStore;
    private readonly ObservableCollection<ProviderRow> providers = [];

    public ProvidersPage()
    {
        InitializeComponent();
        apiClient = IPlatformApplication.Current!.Services.GetRequiredService<CompanyMobileApiClient>();
        sessionStore = IPlatformApplication.Current.Services.GetRequiredService<CompanySessionStore>();
        ProvidersView.ItemsSource = providers;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var token = await sessionStore.GetTokenAsync();
        var companyId = await sessionStore.GetCompanyIdAsync();
        if (string.IsNullOrWhiteSpace(token) || !companyId.HasValue) return;
        var result = await apiClient.GetProvidersAsync(token, companyId.Value);
        if (!result.IsSuccess) return;
        providers.Clear();
        foreach (var provider in (result.Response ?? []).OrderByDescending(item => item.IsAvailable).ThenBy(item => item.FirstName))
        {
            providers.Add(ProviderRow.From(provider));
        }
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadAsync();
        RefreshHost.IsRefreshing = false;
    }

    private async void OnCallClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is ProviderRow row && !string.IsNullOrWhiteSpace(row.PhoneNumber))
        {
            await Launcher.Default.OpenAsync($"tel:{row.PhoneNumber}");
        }
    }

    private async void OnMessageClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is ProviderRow row && !string.IsNullOrWhiteSpace(row.PhoneNumber))
        {
            await Launcher.Default.OpenAsync($"sms:{row.PhoneNumber}");
        }
    }

    public sealed record ProviderRow(
        Guid Id,
        string FullName,
        string PhoneNumber,
        string ServiceLabel,
        string MissionLabel,
        string AvailabilityLabel,
        Color AvailabilityColor)
    {
        public static ProviderRow From(CompanyEmployeeResponse provider)
        {
            var services = string.Join(", ", provider.Services.Take(2).Select(item => item.ServiceName));
            return new ProviderRow(
                provider.Id,
                $"{provider.FirstName} {provider.LastName}".Trim(),
                provider.PhoneNumber,
                string.IsNullOrWhiteSpace(services) ? "Services à compléter" : services,
                provider.CurrentMission is null ? $"{provider.CompletedMissionCount} missions terminées" : provider.CurrentMission.ServiceName,
                provider.CurrentMission is not null ? "EN MISSION" : provider.IsAvailable ? "DISPONIBLE" : "HORS LIGNE",
                provider.CurrentMission is not null
                    ? Color.FromArgb("#155EEF")
                    : provider.IsAvailable ? Color.FromArgb("#16B364") : Color.FromArgb("#667085"));
        }
    }
}
