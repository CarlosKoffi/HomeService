using HomeService.Contracts.ProviderPortal;
using HomeService.Provider.Mobile.Services;
using HomeService.Mobile.Shared;
using Microsoft.Maui.Controls.Shapes;

namespace HomeService.Provider.Mobile.Pages;

public partial class MissionsPage : ContentPage
{
    private readonly ProviderMobileApiClient? apiClient;
    private readonly ProviderSessionService? sessionService;
    private readonly CatalogMediaResolver? catalogMedia;
    private IReadOnlyList<ProviderMobileMissionSummaryResponse> missions = [];
    private MissionFilter activeFilter = MissionFilter.InProgress;

    public MissionsPage()
    {
        InitializeComponent();
        apiClient = IPlatformApplication.Current?.Services.GetService<ProviderMobileApiClient>();
        sessionService = IPlatformApplication.Current?.Services.GetService<ProviderSessionService>();
        catalogMedia = IPlatformApplication.Current?.Services.GetService<CatalogMediaResolver>();
        UpdateFilterStyles();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadMissionsAsync();
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadMissionsAsync();
        RefreshHost.IsRefreshing = false;
    }

    private async Task LoadMissionsAsync()
    {
        MessageBanner.IsVisible = false;
        SetLoading(true);
        var token = sessionService is null ? null : await sessionService.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token) || apiClient is null)
        {
            SetLoading(false);
            ShowMessage("Votre session a expiré. Reconnectez-vous pour consulter vos missions.");
            missions = [];
            await RenderActiveFilterAsync();
            return;
        }

        var result = await apiClient.GetMissionsAsync(token);
        SetLoading(false);
        if (!result.IsSuccess || result.Response is null)
        {
            ShowMessage(result.ErrorMessage ?? "Impossible de charger les missions depuis l’API.");
            missions = [];
            await RenderActiveFilterAsync();
            return;
        }

        missions = result.Response.Items;
        if (activeFilter == MissionFilter.InProgress
            && CountFor(MissionFilter.InProgress) == 0
            && CountFor(MissionFilter.Available) > 0)
        {
            activeFilter = MissionFilter.Available;
        }

        UpdateFilterStyles();
        await RenderActiveFilterAsync();
    }

    private async void OnFilterTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not string value || !Enum.TryParse<MissionFilter>(value, out var filter)) return;
        activeFilter = filter;
        UpdateFilterStyles();
        await RenderActiveFilterAsync();
    }

    private async Task RenderActiveFilterAsync()
    {
        var items = missions.Where(item => Matches(item, activeFilter));
        items = activeFilter is MissionFilter.Completed or MissionFilter.Cancelled
            ? items.OrderByDescending(item => item.ScheduledFor ?? DateTimeOffset.MinValue)
            : items.OrderBy(item => item.ScheduledFor ?? DateTimeOffset.MaxValue);

        var filtered = items.ToList();
        SectionTitleLabel.Text = FilterTitle(activeFilter);
        SectionCountLabel.Text = filtered.Count == 1 ? "1 mission" : $"{filtered.Count} missions";
        MissionListStack.Children.Clear();

        if (filtered.Count == 0)
        {
            MissionListStack.Add(CreateEmptyState(activeFilter));
            return;
        }

        var cards = await Task.WhenAll(filtered.Select(CreateMissionCardAsync));
        foreach (var card in cards)
        {
            MissionListStack.Add(card);
        }
    }

    private async Task<View> CreateMissionCardAsync(ProviderMobileMissionSummaryResponse mission)
    {
        var image = new Image { Source = ProviderIconResolver.ForService(mission.ServiceIconName, mission.ServiceName), WidthRequest = 34, HeightRequest = 34, Aspect = Aspect.AspectFit };
        if (catalogMedia is not null)
        {
            var remote = string.IsNullOrWhiteSpace(mission.PrestationName)
                ? await catalogMedia.ResolveServiceAsync(null, mission.ServiceName)
                : await catalogMedia.ResolvePrestationAsync(null, mission.PrestationName, serviceName: mission.ServiceName);
            if (remote is not null) image.Source = remote;
        }
        var icon = new Border
        {
            WidthRequest = 50,
            HeightRequest = 50,
            BackgroundColor = Color.FromArgb("#EEF4FF"),
            Stroke = Color.FromArgb("#DCE8FF"),
            StrokeShape = new RoundRectangle { CornerRadius = 15 },
            Content = image
        };

        var title = string.IsNullOrWhiteSpace(mission.PrestationName) ? mission.ServiceName : mission.PrestationName;
        var subtitle = string.IsNullOrWhiteSpace(mission.PrestationName) ? mission.CompanyName : mission.ServiceName;
        var content = new VerticalStackLayout
        {
            Spacing = 3,
            Children =
            {
                new Label { Text = title, FontFamily = "PlusJakartaSans", FontAttributes = FontAttributes.Bold, FontSize = 15, LineBreakMode = LineBreakMode.TailTruncation },
                new Label { Text = subtitle, FontFamily = "PlusJakartaSans", FontSize = 12, TextColor = Color.FromArgb("#667085"), LineBreakMode = LineBreakMode.TailTruncation },
                new Label { Text = $"{FormatMissionTime(mission.ScheduledFor)} · {mission.LocationLabel}", FontFamily = "PlusJakartaSans", FontSize = 12, TextColor = Color.FromArgb("#667085"), LineBreakMode = LineBreakMode.TailTruncation },
                new Label { Text = $"{StatusLabel(mission.Status)}  ·  {mission.MissionNumber}", FontFamily = "PlusJakartaSans", FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = StatusColor(mission.Status) }
            }
        };

        var grid = new Grid { ColumnDefinitions = Columns(GridLength.Auto, GridLength.Star, GridLength.Auto), ColumnSpacing = 12 };
        grid.Add(icon, 0);
        grid.Add(content, 1);
        grid.Add(new Image { Source = "chevron_right.svg", WidthRequest = 18, HeightRequest = 18, VerticalOptions = LayoutOptions.Center }, 2);

        var card = new Border
        {
            Style = (Style)Application.Current!.Resources["PremiumCard"],
            Padding = 14,
            Content = grid
        };
        var tap = new TapGestureRecognizer { CommandParameter = mission.AssignmentId };
        tap.Tapped += OnMissionCardTapped;
        card.GestureRecognizers.Add(tap);
        return card;
    }

    private static View CreateEmptyState(MissionFilter filter) => new Border
    {
        Style = (Style)Application.Current!.Resources["PremiumCard"],
        Content = new VerticalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.Fill,
            Children =
            {
                new Image { Source = filter == MissionFilter.Upcoming ? "icon_calendar.svg" : "icon_mission.svg", WidthRequest = 44, HeightRequest = 44, HorizontalOptions = LayoutOptions.Center },
                new Label { Text = EmptyTitle(filter), FontFamily = "PlusJakartaSans", FontSize = 16, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = EmptyMessage(filter), FontFamily = "PlusJakartaSans", FontSize = 13, TextColor = Color.FromArgb("#667085"), HorizontalTextAlignment = TextAlignment.Center }
            }
        }
    };

    private async void OnMissionCardTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is Guid assignmentId)
        {
            await Shell.Current.GoToAsync($"{nameof(MissionDetailPage)}?assignmentId={assignmentId:D}");
        }
    }

    private void UpdateFilterStyles()
    {
        UpdateChip(AvailableChip, AvailableChipLabel, MissionFilter.Available, "Disponibles");
        UpdateChip(InProgressChip, InProgressChipLabel, MissionFilter.InProgress, "En cours");
        UpdateChip(UpcomingChip, UpcomingChipLabel, MissionFilter.Upcoming, "À venir");
        UpdateChip(CompletedChip, CompletedChipLabel, MissionFilter.Completed, "Terminées");
        UpdateChip(CancelledChip, CancelledChipLabel, MissionFilter.Cancelled, "Annulées");
    }

    private void UpdateChip(Border border, Label label, MissionFilter filter, string title)
    {
        var selected = activeFilter == filter;
        border.BackgroundColor = Color.FromArgb(selected ? "#155EEF" : "#FFFFFF");
        border.Stroke = Color.FromArgb(selected ? "#155EEF" : "#DCE8FF");
        label.TextColor = Color.FromArgb(selected ? "#FFFFFF" : "#0F172A");
        label.Text = $"{title} ({CountFor(filter)})";
    }

    private int CountFor(MissionFilter filter) => missions.Count(item => Matches(item, filter));

    private static bool Matches(ProviderMobileMissionSummaryResponse mission, MissionFilter filter)
    {
        var isFuture = mission.ScheduledFor?.LocalDateTime > DateTime.Now;
        return filter switch
        {
            MissionFilter.Available => mission.Status == "Offered",
            MissionFilter.InProgress => mission.Status == "Started" || (mission.Status == "Accepted" && isFuture != true),
            MissionFilter.Upcoming => mission.Status == "Accepted" && isFuture == true,
            MissionFilter.Completed => mission.Status == "Completed",
            MissionFilter.Cancelled => mission.Status is "Cancelled" or "Refused" or "Expired",
            _ => false
        };
    }

    private static string FilterTitle(MissionFilter filter) => filter switch
    {
        MissionFilter.Available => "Missions disponibles",
        MissionFilter.InProgress => "Missions en cours",
        MissionFilter.Upcoming => "Prochaines missions",
        MissionFilter.Completed => "Missions terminées",
        MissionFilter.Cancelled => "Missions annulées ou refusées",
        _ => "Missions"
    };

    private static string EmptyTitle(MissionFilter filter) => filter switch
    {
        MissionFilter.Available => "Aucune mission disponible",
        MissionFilter.InProgress => "Aucune mission en cours",
        MissionFilter.Upcoming => "Aucun rendez-vous à venir",
        MissionFilter.Completed => "Aucune mission terminée",
        MissionFilter.Cancelled => "Aucune mission annulée",
        _ => "Aucune mission"
    };

    private static string EmptyMessage(MissionFilter filter) => filter switch
    {
        MissionFilter.Available => "Restez disponible : les nouvelles propositions apparaîtront ici.",
        MissionFilter.InProgress => "Une mission acceptée apparaît ici dès que son horaire est atteint.",
        MissionFilter.Upcoming => "Les missions acceptées et planifiées seront rangées ici.",
        MissionFilter.Completed => "Votre historique de missions terminées apparaîtra ici.",
        MissionFilter.Cancelled => "Les missions refusées, expirées ou annulées apparaîtront ici.",
        _ => string.Empty
    };

    private static string FormatMissionTime(DateTimeOffset? value) => value?.LocalDateTime.ToString("ddd d MMM · HH:mm") ?? "Horaire à confirmer";

    private static string StatusLabel(string status) => status switch
    {
        "Offered" => "À confirmer",
        "Accepted" => "Acceptée",
        "Started" => "En cours",
        "Completed" => "Terminée",
        "Cancelled" => "Annulée",
        "Refused" => "Refusée",
        "Expired" => "Expirée",
        _ => status
    };

    private static Color StatusColor(string status) => status switch
    {
        "Completed" => Color.FromArgb("#067647"),
        "Cancelled" or "Refused" or "Expired" => Color.FromArgb("#B42318"),
        "Offered" => Color.FromArgb("#B54708"),
        _ => Color.FromArgb("#155EEF")
    };

    private void SetLoading(bool loading)
    {
        LoadingIndicator.IsVisible = loading;
        LoadingIndicator.IsRunning = loading;
    }

    private void ShowMessage(string message)
    {
        MessageLabel.Text = message;
        MessageBanner.IsVisible = true;
    }

    private static ColumnDefinitionCollection Columns(params GridLength[] widths)
    {
        var result = new ColumnDefinitionCollection();
        foreach (var width in widths) result.Add(new ColumnDefinition(width));
        return result;
    }

    private enum MissionFilter
    {
        Available,
        InProgress,
        Upcoming,
        Completed,
        Cancelled
    }
}
