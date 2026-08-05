using System.Collections.ObjectModel;
using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

public partial class RequestsPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient;
    private readonly ClientSessionStore sessionStore;
    private readonly ObservableCollection<MissionRow> missions = [];
    private string? currentStatus = "Active";

    public RequestsPage()
    {
        InitializeComponent();
        apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
        sessionStore = MobileServiceLocator.GetRequiredService<ClientSessionStore>();
        MissionsView.ItemsSource = missions;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        currentStatus = "Active";
        SetFilter(ActiveButton);
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        missions.Clear();
        if (!sessionStore.HasSession())
        {
            EmptyState.IsVisible = true;
            return;
        }

        if (sessionStore.IsPreviewMode())
        {
            AddPreviewMissions();
            EmptyState.IsVisible = false;
            return;
        }

        var result = await apiClient.GetMissionsAsync(currentStatus);
        if (result.IsSuccess && result.Response is not null)
        {
            foreach (var item in result.Response)
            {
                var image = await apiClient.DownloadMediaImageSourceAsync(item.IconUrl);
                missions.Add(MissionRow.From(item, image));
            }
        }

        EmptyState.IsVisible = missions.Count == 0;
    }

    private void AddPreviewMissions()
    {
        var all = new[]
        {
            MissionRow.Preview(Guid.Parse("11111111-1111-1111-1111-111111111111"), "WL-000145 - Déboucher un évier", "Cocody, Riviera 3", "Aujourd'hui 14:30", "En cours", "17 000 FCFA", "\uD83D\uDD27"),
            MissionRow.Preview(Guid.Parse("22222222-2222-2222-2222-222222222222"), "WL-000132 - Nettoyage canapé", "Marcory Zone 4", "Hier 16:10", "Terminée", "25 000 FCFA", "\uD83E\uDDF9"),
            MissionRow.Preview(Guid.Parse("33333333-3333-3333-3333-333333333333"), "WL-000128 - Réparation murale", "Angré 8e tranche", "22/07 10:00", "Annulée", "18 000 FCFA", "\uD83C\uDFE0")
        };

        foreach (var mission in all.Where(MatchesCurrentFilter))
        {
            missions.Add(mission);
        }
    }

    private bool MatchesCurrentFilter(MissionRow mission)
    {
        return currentStatus switch
        {
            "Active" => mission.StatusLabel == "En cours",
            "Past" => mission.StatusLabel == "Terminée",
            "Cancelled" => mission.StatusLabel == "Annulée",
            _ => true
        };
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        await LoadAsync();
        Refresh.IsRefreshing = false;
    }

    private async void OnAllClicked(object sender, EventArgs e)
    {
        currentStatus = null;
        SetFilter(AllButton);
        await LoadAsync();
    }

    private async void OnActiveClicked(object sender, EventArgs e)
    {
        currentStatus = "Active";
        SetFilter(ActiveButton);
        await LoadAsync();
    }

    private async void OnPastClicked(object sender, EventArgs e)
    {
        currentStatus = "Past";
        SetFilter(PastButton);
        await LoadAsync();
    }

    private async void OnCanceledClicked(object sender, EventArgs e)
    {
        currentStatus = "Cancelled";
        SetFilter(CanceledButton);
        await LoadAsync();
    }

    private async void OnMissionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not MissionRow mission)
        {
            return;
        }

        MissionsView.SelectedItem = null;
        await Shell.Current.GoToAsync($"{nameof(MissionDetailPage)}?missionId={mission.MissionId:D}");
    }

    private void SetFilter(Button selected)
    {
        var ink = (Color)Application.Current!.Resources["Ink"];
        var blue = (Color)Application.Current.Resources["WeleBlue"];
        var buttons = new[] { AllButton, ActiveButton, PastButton, CanceledButton };
        foreach (var button in buttons)
        {
            button.BackgroundColor = button == selected ? blue : Colors.White;
            button.TextColor = button == selected ? Colors.White : ink;
            button.BorderColor = button == selected ? blue : (Color)Application.Current.Resources["Line"];
        }
    }

    private sealed record MissionRow(
        Guid MissionId,
        string Title,
        string Address,
        string Schedule,
        string StatusLabel,
        string Amount,
        ImageSource? IconSource,
        bool HasImage,
        Color StatusColor,
        Color StatusBackground)
    {
        public static MissionRow Preview(Guid missionId, string title, string address, string schedule, string status, string amount, string icon)
        {
            var (label, color, background) = ResolveStatus(status);
            return new MissionRow(missionId, title, address, schedule, label, amount, ImageSource.FromFile(icon), true, color, background);
        }

        public static MissionRow From(ClientMissionListItemResponse item, ImageSource? image)
        {
            var title = $"{item.MissionNumber} - {item.PrestationName ?? item.ServiceName ?? "Service"}";
            var schedule = item.ScheduledFor.HasValue
                ? AppointmentDisplayFormatter.FormatWindow(item.ScheduledFor.Value, "dd/MM")
                : item.CreatedAt.ToString("dd/MM HH:mm");
            var amount = item.Amount.HasValue ? $"{item.Amount:N0} {item.Currency}" : "Prix à venir";
            var (label, color, background) = ResolveStatus(item.Status);
            return new MissionRow(item.MissionId, title, item.ServiceAddress ?? "Adresse à confirmer", schedule, label, amount, image, image is not null, color, background);
        }

        private static (string Label, Color Color, Color Background) ResolveStatus(string status)
        {
            var normalized = status.ToLowerInvariant();
            if (normalized.Contains("cancel") || normalized.Contains("annul"))
            {
                return ("Annulée", Color.FromArgb("#DC2626"), Color.FromArgb("#FEF2F2"));
            }

            if (normalized.Contains("complete") || normalized.Contains("finish") || normalized.Contains("term"))
            {
                return ("Terminée", Color.FromArgb("#059669"), Color.FromArgb("#ECFDF5"));
            }

            if (normalized.Contains("pending") || normalized.Contains("quote") || normalized.Contains("wait"))
            {
                return ("À valider", Color.FromArgb("#F97316"), Color.FromArgb("#FFF7ED"));
            }

            return ("En cours", Color.FromArgb("#2563EB"), Color.FromArgb("#EFF6FF"));
        }

    }
}
