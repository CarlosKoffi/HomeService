using System.Collections.ObjectModel;
using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

public partial class MessagesPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient;
    private readonly ClientSessionStore sessionStore;
    private readonly ObservableCollection<ClientConversationRow> conversations = [];
    private bool isLoading;

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
        await LoadConversationsSafelyAsync();
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        await LoadConversationsSafelyAsync(force: true);
        MessagesRefreshView.IsRefreshing = false;
    }

    private async Task LoadConversationsSafelyAsync(bool force = false)
    {
        if (isLoading && !force)
        {
            return;
        }

        isLoading = true;
        ErrorLabel.IsVisible = false;
        try
        {
            conversations.Clear();
            if (!sessionStore.HasSession())
            {
                ShowError("Connectez-vous pour consulter vos conversations.");
                return;
            }

            if (sessionStore.IsPreviewMode())
            {
                conversations.Add(new ClientConversationRow(
                    Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    "Déboucher un évier",
                    "WL-000145",
                    "Ouvrir les échanges de cette demande",
                    "Aujourd'hui"));
                return;
            }

            var result = await apiClient.GetMissionsAsync();
            if (!result.IsSuccess || result.Response is null)
            {
                ShowError(result.ErrorMessage ?? "Impossible de charger vos conversations.");
                return;
            }

            foreach (var mission in result.Response.OrderByDescending(item => item.CreatedAt))
            {
                conversations.Add(ClientConversationRow.From(mission));
            }
        }
        catch
        {
            ShowError("Impossible de charger vos messages. Vérifiez votre connexion puis réessayez.");
        }
        finally
        {
            isLoading = false;
        }
    }

    private async void OnConversationSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ClientConversationRow conversation)
        {
            return;
        }

        ConversationsView.SelectedItem = null;
        try
        {
            await Shell.Current.GoToAsync($"{nameof(MissionChatPage)}?missionId={conversation.MissionId:D}");
        }
        catch
        {
            ShowError("Cette conversation ne peut pas être ouverte pour le moment.");
        }
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}

public sealed record ClientConversationRow(Guid MissionId, string Title, string MissionNumber, string Hint, string DateLabel)
{
    public static ClientConversationRow From(ClientMissionListItemResponse response)
    {
        var title = response.PrestationName ?? response.ServiceName ?? "Demande de service";
        var date = (response.ScheduledFor ?? response.CreatedAt).ToLocalTime().ToString("dd MMM · HH:mm");
        return new ClientConversationRow(response.MissionId, title, response.MissionNumber, "Ouvrir la conversation", date);
    }
}
