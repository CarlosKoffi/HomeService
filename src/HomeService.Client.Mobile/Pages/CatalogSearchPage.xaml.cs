using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

public partial class CatalogSearchPage : ContentPage
{
    private const int PreviewCount = 3;
    private readonly ClientMobileApiClient apiClient;
    private readonly List<SearchItem> results = [];
    private CancellationTokenSource? searchCancellation;
    private SearchTab selectedTab = SearchTab.All;
    private bool showAll;

    public CatalogSearchPage()
    {
        InitializeComponent();
        apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(150), () => SearchEntry.Focus());
    }

    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        searchCancellation?.Cancel();
        searchCancellation?.Dispose();
        searchCancellation = new CancellationTokenSource();
        var cancellationToken = searchCancellation.Token;

        try
        {
            await Task.Delay(250, cancellationToken);
            await SearchAsync(e.NewTextValue?.Trim() ?? string.Empty, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task SearchAsync(string query, CancellationToken cancellationToken)
    {
        results.Clear();
        showAll = false;
        if (string.IsNullOrWhiteSpace(query))
        {
            Render();
            return;
        }

        SetLoading(true);
        var response = await apiClient.SearchCatalogAsync(query, cancellationToken);
        if (response.IsSuccess && response.Response is not null)
        {
            results.AddRange(response.Response.Select(item => SearchItem.From(item, apiClient)));
        }

        SetLoading(false);
        Render();
    }

    private void Render()
    {
        ServicesList.Clear();
        PrestationsList.Clear();

        var services = results.GroupBy(item => item.ServiceId)
            .Select(group => group.OrderByDescending(item => item.HasImage).First())
            .ToList();
        var prestations = results.Where(item => item.PrestationId.HasValue).ToList();

        if (selectedTab is SearchTab.All or SearchTab.Services)
        {
            foreach (var item in showAll ? services : services.Take(PreviewCount))
            {
                ServicesList.Add(CreateTextRow(item with
                {
                    Name = item.ServiceName,
                    PrestationId = null
                }));
            }
        }

        if (selectedTab is SearchTab.All or SearchTab.Prestations)
        {
            foreach (var item in showAll ? prestations : prestations.Take(PreviewCount))
            {
                PrestationsList.Add(CreateIllustratedRow(item));
            }
        }

        ServicesSection.IsVisible = selectedTab is not SearchTab.Prestations && services.Count > 0;
        PrestationsSection.IsVisible = selectedTab is not SearchTab.Services && prestations.Count > 0;
        var count = selectedTab switch
        {
            SearchTab.Services => services.Count,
            SearchTab.Prestations => prestations.Count,
            _ => results.Count
        };
        ShowAllButton.Text = $"Voir tous les résultats ({count})";
        ShowAllButton.IsVisible = count > PreviewCount && !showAll;
        EmptyLabel.IsVisible = !string.IsNullOrWhiteSpace(SearchEntry.Text) && count == 0 && !LoadingIndicator.IsRunning;
    }

    private View CreateIllustratedRow(SearchItem item)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(52)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(24))
            },
            ColumnSpacing = 11,
            Padding = new Thickness(0, 3)
        };
        row.Add(new Border
        {
            HeightRequest = 52,
            WidthRequest = 52,
            Padding = 0,
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 9 },
            BackgroundColor = Color.FromArgb("#F1F4FA"),
            Content = CreateMedia(item)
        });
        var labels = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        labels.Add(new Label { Text = item.Name, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#111827") });
        labels.Add(new Label { Text = item.ServiceName, FontSize = 11, TextColor = Color.FromArgb("#687386") });
        row.Add(labels, 1);
        row.Add(new Label { Text = "›", FontSize = 24, TextColor = Color.FromArgb("#303949"), VerticalTextAlignment = TextAlignment.Center }, 2);
        AddOpenGesture(row, item);
        return row;
    }

    private View CreateTextRow(SearchItem item)
    {
        var row = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(new GridLength(28)), new ColumnDefinition(GridLength.Star) },
            HeightRequest = 44,
            Padding = new Thickness(11, 0),
            ColumnSpacing = 4
        };
        row.Add(new Label { Text = "⊙", FontSize = 16, TextColor = Color.FromArgb("#687386"), VerticalTextAlignment = TextAlignment.Center });
        row.Add(new Label
        {
            Text = item.Name,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#303949"),
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        }, 1);
        var border = new Border
        {
            Stroke = Color.FromArgb("#E6EAF1"),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            Content = row
        };
        AddOpenGesture(border, item);
        return border;
    }

    private static View CreateMedia(SearchItem item)
    {
        if (item.HasImage)
        {
            return new Image { Source = item.ImageUrl, Aspect = Aspect.AspectFill };
        }
        if (item.HasIcon)
        {
            return new Image { Source = item.IconUrl, Aspect = Aspect.AspectFit, Margin = new Thickness(11) };
        }
        return new Label
        {
            Text = item.Fallback,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#2563EB"),
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };
    }

    private void AddOpenGesture(View view, SearchItem item)
    {
        var gesture = new TapGestureRecognizer();
        gesture.Tapped += async (_, _) => await OpenItemAsync(item);
        view.GestureRecognizers.Add(gesture);
    }

    private static Task OpenItemAsync(SearchItem item)
    {
        var path = $"{nameof(CreateRequestPage)}?serviceId={item.ServiceId:D}&prestationId={item.PrestationId?.ToString("D") ?? string.Empty}&name={Uri.EscapeDataString(item.Name)}";
        return Shell.Current.GoToAsync(path);
    }

    private void SelectTab(SearchTab tab)
    {
        selectedTab = tab;
        showAll = false;
        SetTabStyle(AllTabLabel, AllTabIndicator, tab == SearchTab.All);
        SetTabStyle(ServicesTabLabel, ServicesTabIndicator, tab == SearchTab.Services);
        SetTabStyle(PrestationsTabLabel, PrestationsTabIndicator, tab == SearchTab.Prestations);
        Render();
    }

    private static void SetTabStyle(Label label, BoxView indicator, bool selected)
    {
        label.TextColor = Color.FromArgb(selected ? "#2563EB" : "#687386");
        indicator.Color = Color.FromArgb(selected ? "#2563EB" : "#FFFFFF");
    }

    private void SetLoading(bool loading)
    {
        LoadingIndicator.IsVisible = loading;
        LoadingIndicator.IsRunning = loading;
    }

    private void OnAllTabTapped(object sender, TappedEventArgs e) => SelectTab(SearchTab.All);
    private void OnServicesTabTapped(object sender, TappedEventArgs e) => SelectTab(SearchTab.Services);
    private void OnPrestationsTabTapped(object sender, TappedEventArgs e) => SelectTab(SearchTab.Prestations);
    private void OnShowAllClicked(object sender, EventArgs e) { showAll = true; Render(); }
    private async void OnCancelClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    private enum SearchTab { All, Services, Prestations }

    private sealed record SearchItem(
        Guid ServiceId, Guid? PrestationId, string Name, string ServiceName,
        string? IconUrl, string? ImageUrl, string Fallback, bool HasIcon, bool HasImage)
    {
        public static SearchItem From(ClientCatalogSearchResultResponse response, ClientMobileApiClient apiClient)
        {
            var name = response.PrestationName ?? response.Name;
            var iconUrl = apiClient.ToAbsoluteMediaUrl(response.IconUrl);
            var imageUrl = apiClient.ToAbsoluteMediaUrl(response.ImageUrl);
            var fallback = string.IsNullOrWhiteSpace(name) ? "WE" : name[..Math.Min(2, name.Length)].ToUpperInvariant();
            return new SearchItem(response.ServiceId, response.PrestationId, name, response.ServiceName,
                iconUrl, imageUrl, fallback, !string.IsNullOrWhiteSpace(iconUrl), !string.IsNullOrWhiteSpace(imageUrl));
        }
    }
}
