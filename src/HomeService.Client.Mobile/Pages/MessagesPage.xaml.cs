using System.Collections.ObjectModel;
using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

public partial class MessagesPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient;
    private readonly ClientSessionStore sessionStore;
    private readonly ObservableCollection<ClientConversationRow> conversations = [];
    private readonly SemaphoreSlim loadGate = new(1, 1);
    private bool isNavigating;

    public MessagesPage()
    {
        InitializeComponent();
        apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
        sessionStore = MobileServiceLocator.GetRequiredService<ClientSessionStore>();
        ConversationsView.ItemsSource = conversations;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        isNavigating = false;
        await LoadConversationsSafelyAsync();
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        await LoadConversationsSafelyAsync();
        MessagesRefreshView.IsRefreshing = false;
    }

    private async Task LoadConversationsSafelyAsync()
    {
        if (!await loadGate.WaitAsync(0))
        {
            return;
        }

        ErrorLabel.IsVisible = false;
        try
        {
            if (!sessionStore.HasSession())
            {
                await ReplaceConversationsAsync([]);
                ShowError("Connectez-vous pour consulter vos conversations.");
                return;
            }

            if (sessionStore.IsPreviewMode())
            {
                await ReplaceConversationsAsync([new ClientConversationRow(
                    Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    "Déboucher un évier",
                    "WL-000145",
                    "Ouvrir les échanges de cette demande",
                    "Aujourd'hui")]);
                return;
            }

            var result = await apiClient.GetMissionsAsync();
            if (!result.IsSuccess || result.Response is null)
            {
                ShowError(result.ErrorMessage ?? "Impossible de charger vos conversations.");
                return;
            }

            var rows = result.Response
                .Where(item => !IsConversationClosed(item.Status))
                .OrderByDescending(item => item.CreatedAt)
                .Select(ClientConversationRow.From)
                .ToArray();
            await ReplaceConversationsAsync(rows);
        }
        catch
        {
            ShowError("Impossible de charger vos messages. Vérifiez votre connexion puis réessayez.");
        }
        finally
        {
            loadGate.Release();
        }
    }

    private static bool IsConversationClosed(string status)
    {
        return string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Resolved", StringComparison.OrdinalIgnoreCase);
    }

    private async void OnConversationTapped(object sender, TappedEventArgs e)
    {
        if (isNavigating || e.Parameter is not ClientConversationRow conversation)
        {
            return;
        }

        isNavigating = true;
        try
        {
            await Shell.Current.GoToAsync($"{nameof(MissionChatPage)}?missionId={conversation.MissionId:D}");
        }
        catch
        {
            isNavigating = false;
            ShowError("Cette conversation ne peut pas être ouverte pour le moment.");
        }
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }

    private Task ReplaceConversationsAsync(IEnumerable<ClientConversationRow> rows)
    {
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            conversations.Clear();
            foreach (var row in rows)
            {
                conversations.Add(row);
            }
        });
    }
}

public sealed record ClientConversationRow(Guid MissionId, string Title, string MissionNumber, string Hint, string DateLabel)
{
    public static ClientConversationRow From(ClientMissionListItemResponse response)
    {
        var title = response.OptionName ?? response.PrestationName ?? response.ServiceName ?? "Demande de service";
        var date = response.ScheduledFor.HasValue
            ? AppointmentDisplayFormatter.FormatWindow(response.ScheduledFor.Value, "dd MMM")
            : response.CreatedAt.ToLocalTime().ToString("dd MMM · HH:mm");
        return new ClientConversationRow(response.MissionId, title, response.MissionNumber, "Ouvrir la conversation", date);
    }
}
