using HomeService.Contracts.ProviderPortal;
using HomeService.Provider.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace HomeService.Provider.Mobile.Pages;

public partial class CalendarPage : ContentPage
{
    private readonly ProviderMobileApiClient? apiClient;
    private readonly ProviderSessionService? sessionService;
    private DateTime visibleMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime selectedDate = DateTime.Today;
    private IReadOnlyList<ProviderMobileMissionSummaryResponse> missions = [];

    public CalendarPage()
    {
        InitializeComponent();
        apiClient = IPlatformApplication.Current?.Services.GetService<ProviderMobileApiClient>();
        sessionService = IPlatformApplication.Current?.Services.GetService<ProviderSessionService>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadAsync();
        RefreshHost.IsRefreshing = false;
    }

    private async Task LoadAsync()
    {
        var token = sessionService is null ? null : await sessionService.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token) || apiClient is null)
        {
            return;
        }

        var from = new DateTimeOffset(visibleMonth.AddDays(-7), TimeZoneInfo.Local.GetUtcOffset(visibleMonth));
        var toDate = visibleMonth.AddMonths(1).AddDays(7);
        var to = new DateTimeOffset(toDate, TimeZoneInfo.Local.GetUtcOffset(toDate));
        var result = await apiClient.GetMissionsAsync(token, from, to);
        missions = result.Response?.Items ?? [];
        RenderCalendar();
        RenderSelectedDay();
    }

    private void RenderCalendar()
    {
        MonthLabel.Text = visibleMonth.ToString("MMMM yyyy");
        CalendarGrid.Children.Clear();
        var offset = ((int)visibleMonth.DayOfWeek + 6) % 7;
        var days = DateTime.DaysInMonth(visibleMonth.Year, visibleMonth.Month);

        for (var day = 1; day <= days; day++)
        {
            var date = new DateTime(visibleMonth.Year, visibleMonth.Month, day);
            var index = offset + day - 1;
            var hasMission = missions.Any(item => item.ScheduledFor?.LocalDateTime.Date == date.Date);
            var selected = date.Date == selectedDate.Date;
            var cell = new Border
            {
                BackgroundColor = selected ? Color.FromArgb("#155EEF") : Colors.White,
                Stroke = selected ? Color.FromArgb("#155EEF") : Colors.Transparent,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Content = new VerticalStackLayout
                {
                    Spacing = 0,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new Label { Text = day.ToString(), FontFamily = "PlusJakartaSans", FontSize = 13, TextColor = selected ? Colors.White : Color.FromArgb("#0F172A"), HorizontalTextAlignment = TextAlignment.Center },
                        new Label { Text = hasMission ? "•" : string.Empty, FontSize = 13, TextColor = selected ? Colors.White : Color.FromArgb("#155EEF"), HorizontalTextAlignment = TextAlignment.Center }
                    }
                }
            };
            var tap = new TapGestureRecognizer { CommandParameter = date };
            tap.Tapped += OnDayTapped;
            cell.GestureRecognizers.Add(tap);
            CalendarGrid.Add(cell, index % 7, index / 7);
        }
    }

    private void RenderSelectedDay()
    {
        SelectedDayLabel.Text = selectedDate.ToString("dddd d MMMM");
        DayMissionsStack.Children.Clear();
        var dayMissions = missions.Where(item => item.ScheduledFor?.LocalDateTime.Date == selectedDate.Date).OrderBy(item => item.ScheduledFor).ToList();
        if (dayMissions.Count == 0)
        {
            DayMissionsStack.Add(new Label { Text = "Aucune mission prévue ce jour.", Style = (Style)Application.Current!.Resources["MutedLabel"] });
            return;
        }

        foreach (var mission in dayMissions)
        {
            var missionGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 14
            };
            missionGrid.Add(new Label { Text = mission.ScheduledFor?.LocalDateTime.ToString("HH:mm") ?? "--:--", FontFamily = "PlusJakartaSans", FontAttributes = FontAttributes.Bold, FontSize = 16, TextColor = Color.FromArgb("#155EEF") }, 0);
            missionGrid.Add(new VerticalStackLayout
            {
                Spacing = 3,
                Children =
                {
                    new Label { Text = mission.ServiceName, FontFamily = "PlusJakartaSans", FontAttributes = FontAttributes.Bold, FontSize = 15 },
                    new Label { Text = mission.LocationLabel, FontFamily = "PlusJakartaSans", FontSize = 12, TextColor = Color.FromArgb("#667085") },
                    new Label { Text = $"Créneau de 30 min · {mission.Status}", FontFamily = "PlusJakartaSans", FontSize = 11, TextColor = Color.FromArgb("#667085") }
                }
            }, 1);
            DayMissionsStack.Add(new Border
            {
                Style = (Style)Application.Current!.Resources["PremiumCard"],
                Content = missionGrid
            });
        }
    }

    private void OnDayTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not DateTime date) return;
        selectedDate = date;
        RenderCalendar();
        RenderSelectedDay();
    }

    private async void OnPreviousMonthClicked(object? sender, EventArgs e) { visibleMonth = visibleMonth.AddMonths(-1); selectedDate = visibleMonth; await LoadAsync(); }
    private async void OnNextMonthClicked(object? sender, EventArgs e) { visibleMonth = visibleMonth.AddMonths(1); selectedDate = visibleMonth; await LoadAsync(); }
}
