using System.Collections.ObjectModel;
using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;
using HomeService.Contracts.Services;

namespace HomeService.Client.Mobile.Pages;

public partial class HomePage : ContentPage
{
    private readonly ClientMobileApiClient apiClient;
    private readonly ClientSessionStore sessionStore;
    private readonly ObservableCollection<ServiceItem> services = [];
    private readonly ObservableCollection<SearchResultItem> searchResults = [];
    private readonly ObservableCollection<MissionItem> missions = [];

    public HomePage()
    {
        InitializeComponent();
        apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
        sessionStore = MobileServiceLocator.GetRequiredService<ClientSessionStore>();
        ServicesView.ItemsSource = services;
        SearchResultsView.ItemsSource = searchResults;
        MissionsView.ItemsSource = missions;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        GreetingLabel.Text = sessionStore.HasSession()
            ? $"Bonjour {sessionStore.GetDisplayName()}"
            : "Bonjour, trouvez rapidement un service a domicile";
        LoginCard.IsVisible = !sessionStore.HasSession();

        var serviceResult = await apiClient.GetServicesAsync();
        if (serviceResult.IsSuccess && serviceResult.Response is not null)
        {
            services.Clear();
            foreach (var service in serviceResult.Response.Where(service => service.IsActive).Take(8))
            {
                services.Add(ServiceItem.From(service, apiClient));
            }
        }

        missions.Clear();
        if (sessionStore.HasSession())
        {
            var missionResult = await apiClient.GetMissionsAsync();
            if (missionResult.IsSuccess && missionResult.Response is not null)
            {
                foreach (var mission in missionResult.Response.Take(4))
                {
                    missions.Add(MissionItem.From(mission));
                }
            }
        }

        MissionSection.IsVisible = missions.Count > 0;
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        await LoadAsync();
        Refresh.IsRefreshing = false;
    }

    private async void OnSearchClicked(object sender, EventArgs e)
    {
        var query = SearchEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            SearchResultsSection.IsVisible = false;
            return;
        }

        var result = await apiClient.SearchCatalogAsync(query);
        searchResults.Clear();
        if (result.IsSuccess && result.Response is not null)
        {
            foreach (var item in result.Response.Take(12))
            {
                searchResults.Add(SearchResultItem.From(item, apiClient));
            }
        }

        SearchResultsSection.IsVisible = searchResults.Count > 0;
    }

    private void OnSearchCompleted(object sender, EventArgs e)
    {
        OnSearchClicked(sender, e);
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(LoginPage));
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(RegisterPage));
    }

    private async void OnProfileClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//profile");
    }

    private async void OnMissionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not MissionItem mission)
        {
            return;
        }

        MissionsView.SelectedItem = null;
        await Shell.Current.GoToAsync($"{nameof(MissionDetailPage)}?missionId={mission.MissionId:D}");
    }

    private async void OnSearchResultSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not SearchResultItem item)
        {
            return;
        }

        SearchResultsView.SelectedItem = null;
        var path = $"{nameof(CreateRequestPage)}?serviceId={item.ServiceId:D}&prestationId={item.PrestationId?.ToString("D") ?? string.Empty}&name={Uri.EscapeDataString(item.Name)}";
        await Shell.Current.GoToAsync(path);
    }

    private sealed record ServiceItem(string Name, string? IconUrl, string IconFallback, bool HasIconUrl, bool HasIconFallback, string Price)
    {
        public static ServiceItem From(ServiceSummaryResponse response, ClientMobileApiClient apiClient)
        {
            var price = response.PriceMinAmount.HasValue
                ? $"A partir de {response.PriceMinAmount:N0} {response.Currency}"
                : $"{response.NormalPriceAmount:N0} {response.Currency}";

            var iconUrl = apiClient.ToAbsoluteMediaUrl(response.IconUrl);
            var fallback = ResolveIcon(response.IconName, response.Name);
            return new ServiceItem(response.Name, iconUrl, fallback, !string.IsNullOrWhiteSpace(iconUrl), string.IsNullOrWhiteSpace(iconUrl), price);
        }

        private static string ResolveIcon(string iconName, string name)
        {
            var normalized = $"{iconName} {name}".ToLowerInvariant();
            if (normalized.Contains("garden") || normalized.Contains("jardin")) return "JA";
            if (normalized.Contains("clean") || normalized.Contains("menage")) return "ME";
            if (normalized.Contains("electric")) return "EL";
            if (normalized.Contains("auto")) return "AU";
            if (normalized.Contains("laundry") || normalized.Contains("blanch")) return "BL";
            return "WE";
        }
    }

    private sealed record SearchResultItem(Guid ServiceId, Guid? PrestationId, string Name, string Service, string Price, string? IconUrl, string IconFallback, bool HasIconUrl, bool HasIconFallback)
    {
        public static SearchResultItem From(ClientCatalogSearchResultResponse response, ClientMobileApiClient apiClient)
        {
            var label = response.PrestationName ?? response.Name;
            var service = response.PrestationName is null ? response.ServiceName : $"{response.ServiceName} - {response.PrestationName}";
            var price = response.PriceMinAmount.HasValue
                ? $"des {response.PriceMinAmount:N0} {response.Currency}"
                : "Prix a confirmer";

            var iconUrl = apiClient.ToAbsoluteMediaUrl(response.IconUrl);
            var fallback = string.IsNullOrWhiteSpace(response.ServiceName)
                ? "WE"
                : response.ServiceName[..Math.Min(2, response.ServiceName.Length)].ToUpperInvariant();

            return new SearchResultItem(response.ServiceId, response.PrestationId, label, service, price, iconUrl, fallback, !string.IsNullOrWhiteSpace(iconUrl), string.IsNullOrWhiteSpace(iconUrl));
        }
    }

    private sealed record MissionItem(Guid MissionId, string Title, string Subtitle, string Status)
    {
        public static MissionItem From(ClientMissionListItemResponse response)
        {
            var title = $"{response.MissionNumber} - {response.PrestationName ?? response.ServiceName ?? "Mission"}";
            var subtitle = response.ScheduledFor.HasValue
                ? response.ScheduledFor.Value.ToString("dd/MM/yyyy HH:mm")
                : response.CreatedAt.ToString("dd/MM/yyyy HH:mm");

            return new MissionItem(response.MissionId, title, subtitle, response.PrimaryAction);
        }
    }
}
