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
    private readonly ObservableCollection<ServiceItem> homeServices = [];
    private readonly ObservableCollection<ServiceItem> wellbeingServices = [];
    private readonly ObservableCollection<PopularItem> popularServices = [];
    private readonly ObservableCollection<SearchResultItem> searchResults = [];

    public HomePage()
    {
        InitializeComponent();
        apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
        sessionStore = MobileServiceLocator.GetRequiredService<ClientSessionStore>();
        HomeServicesView.ItemsSource = homeServices;
        WellbeingServicesView.ItemsSource = wellbeingServices;
        PopularServicesView.ItemsSource = popularServices;
        SearchResultsView.ItemsSource = searchResults;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadSafelyAsync();
    }

    private async Task LoadAsync()
    {
        LoadErrorPanel.IsVisible = false;
        GreetingLabel.Text = sessionStore.HasSession()
            ? $"Bonjour {sessionStore.GetDisplayName()} 👋"
            : "Bonjour 👋";

        var result = await apiClient.GetServicesAsync();
        if (!result.IsSuccess || result.Response is null)
        {
            LoadErrorLabel.Text = result.ErrorMessage ?? "Impossible de charger les services.";
            LoadErrorPanel.IsVisible = true;
            return;
        }

        services.Clear();
        homeServices.Clear();
        wellbeingServices.Clear();
        popularServices.Clear();

        var serviceItems = await Task.WhenAll(result.Response
            .Where(item => item.IsActive)
            .Select(service => ServiceItem.FromAsync(service, apiClient)));
        foreach (var service in serviceItems)
        {
            services.Add(service);
        }

        foreach (var item in services.Where(item => item.DisplayCategory == "Home"))
        {
            homeServices.Add(item);
        }

        foreach (var item in services.Where(item => item.DisplayCategory == "Wellbeing"))
        {
            wellbeingServices.Add(item);
        }

        var mostRequestedPrestations = result.Response
            .Where(service => service.IsActive)
            .SelectMany(service => service.Prestations
                .Where(prestation => prestation.IsActive)
                .Select(prestation => new { Service = service, Prestation = prestation }))
            .OrderByDescending(item => item.Prestation.MissionCount)
            .ThenBy(item => item.Prestation.Name)
            .Take(3);

        var popularItems = await Task.WhenAll(mostRequestedPrestations.Select(async item =>
        {
            var illustrationUrl = await apiClient.DownloadMediaImageSourceAsync(item.Prestation.IllustrationUrl);
            var serviceIconUrl = await apiClient.DownloadMediaImageSourceAsync(item.Service.IconUrl);
            return new PopularItem(
                item.Service.Id,
                item.Prestation.Id,
                item.Prestation.Name,
                illustrationUrl,
                serviceIconUrl,
                string.IsNullOrWhiteSpace(item.Prestation.Name) ? "WE" : item.Prestation.Name[..Math.Min(2, item.Prestation.Name.Length)].ToUpperInvariant(),
                illustrationUrl is not null,
                illustrationUrl is null && serviceIconUrl is not null,
                illustrationUrl is null && serviceIconUrl is null);
        }));
        foreach (var item in popularItems)
        {
            popularServices.Add(item);
        }

        if (popularServices.Count == 0)
        {
            foreach (var service in services.Take(3))
            {
                popularServices.Add(new PopularItem(
                    service.ServiceId,
                    null,
                    service.Name,
                    null,
                    service.IconUrl,
                    service.IconFallback,
                    false,
                    service.HasIconUrl,
                    service.HasIconFallback));
            }
        }
    }

    private async void OnRetryClicked(object sender, EventArgs e)
    {
        await LoadSafelyAsync();
    }

    private async Task LoadSafelyAsync()
    {
        try
        {
            await LoadAsync();
        }
        catch
        {
            LoadErrorLabel.Text = "La page n'a pas pu etre chargee. Verifiez votre connexion puis reessayez.";
            LoadErrorPanel.IsVisible = true;
        }
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

        SearchCountLabel.Text = searchResults.Count <= 1
            ? $"{searchResults.Count} résultat"
            : $"{searchResults.Count} résultats";
        SearchResultsSection.IsVisible = searchResults.Count > 0;
    }

    private void OnSearchCompleted(object sender, EventArgs e)
    {
        OnSearchClicked(sender, e);
    }

    private async void OnSearchTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(CatalogSearchPage));
    }

    private async void OnNotificationsClicked(object sender, EventArgs e)
    {
        await DisplayAlert("Notifications", "Vos notifications apparaîtront ici.", "Fermer");
    }

    private async void OnRequestsTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("//requests");
    }

    private async void OnMessagesTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("//messages");
    }

    private async void OnProfileTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("//profile");
    }

    private async void OnCreateRequestClicked(object sender, EventArgs e)
    {
        if (services.FirstOrDefault() is not ServiceItem service)
        {
            return;
        }

        await OpenServiceAsync(service);
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

    private async void OnServiceSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ServiceItem service)
        {
            return;
        }

        if (sender is CollectionView collectionView)
        {
            collectionView.SelectedItem = null;
        }

        await OpenServiceAsync(service);
    }

    private async void OnPopularSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not PopularItem item)
        {
            return;
        }

        PopularServicesView.SelectedItem = null;
        var path = $"{nameof(CreateRequestPage)}?serviceId={item.ServiceId:D}&prestationId={item.PrestationId?.ToString("D") ?? string.Empty}&name={Uri.EscapeDataString(item.Name)}";
        await Shell.Current.GoToAsync(path);
    }

    private static Task OpenServiceAsync(ServiceItem service)
    {
        var path = $"{nameof(CreateRequestPage)}?serviceId={service.ServiceId:D}&prestationId=&name={Uri.EscapeDataString(service.Name)}";
        return Shell.Current.GoToAsync(path);
    }

    private sealed record ServiceItem(
        Guid ServiceId,
        string Name,
        ImageSource? IconUrl,
        string IconFallback,
        bool HasIconUrl,
        bool HasIconFallback,
        string Price,
        string DisplayCategory)
    {
        public static async Task<ServiceItem> FromAsync(ServiceSummaryResponse response, ClientMobileApiClient apiClient)
        {
            var price = response.PriceMinAmount.HasValue
                ? $"À partir de {response.PriceMinAmount:N0} {response.Currency}"
                : $"{response.NormalPriceAmount:N0} {response.Currency}";

            var iconUrl = await apiClient.DownloadMediaImageSourceAsync(response.IconUrl);
            var fallback = ResolveIcon(response.IconName, response.Name);
            return new ServiceItem(
                response.Id,
                response.Name,
                iconUrl,
                fallback,
                iconUrl is not null,
                iconUrl is null,
                price,
                response.DisplayCategory);
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

    private sealed record SearchResultItem(
        Guid ServiceId,
        Guid? PrestationId,
        string Name,
        string Service,
        string Price,
        ImageSource? IconUrl,
        string IconFallback,
        bool HasIconUrl,
        bool HasIconFallback)
    {
        public static SearchResultItem From(
            ClientCatalogSearchResultResponse response,
            ClientMobileApiClient apiClient)
        {
            var label = response.PrestationName ?? response.Name;
            var service = response.PrestationName is null
                ? response.ServiceName
                : $"{response.ServiceName} - {response.PrestationName}";
            var price = response.PriceMinAmount.HasValue
                ? $"dès {response.PriceMinAmount:N0} {response.Currency}"
                : "Prix à confirmer";

            var iconUrl = apiClient.ToRemoteImageSource(response.IconUrl);
            var fallback = string.IsNullOrWhiteSpace(response.ServiceName)
                ? "WE"
                : response.ServiceName[..Math.Min(2, response.ServiceName.Length)].ToUpperInvariant();

            return new SearchResultItem(
                response.ServiceId,
                response.PrestationId,
                label,
                service,
                price,
                iconUrl,
                fallback,
                iconUrl is not null,
                iconUrl is null);
        }
    }

    private sealed record PopularItem(
        Guid ServiceId,
        Guid? PrestationId,
        string Name,
        ImageSource? IllustrationUrl,
        ImageSource? ServiceIconUrl,
        string Fallback,
        bool HasIllustration,
        bool HasServiceIcon,
        bool HasFallback);
}
