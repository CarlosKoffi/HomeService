using System.Collections.ObjectModel;
using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

[QueryProperty(nameof(MissionId), "missionId")]
[QueryProperty(nameof(Mode), "mode")]
public partial class MessagesPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient;
    private readonly ClientSessionStore sessionStore;
    private readonly ObservableCollection<MessageRow> messages = [];
    private readonly ObservableCollection<ConversationRow> conversations = [];
    private Guid? requestedMissionId;
    private Guid? activeMissionId;
    private string requestedMode = "list";

    public MessagesPage()
    {
        InitializeComponent();
        apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
        sessionStore = MobileServiceLocator.GetRequiredService<ClientSessionStore>();
        MessagesView.ItemsSource = messages;
        ConversationsView.ItemsSource = conversations;
    }

    public string? MissionId
    {
        set => requestedMissionId = Guid.TryParse(value, out var missionId) ? missionId : null;
    }

    public string? Mode
    {
        set
        {
            requestedMode = string.Equals(value, "chat", StringComparison.OrdinalIgnoreCase) ? "chat" : "list";
            if (requestedMode == "list")
            {
                requestedMissionId = null;
                activeMissionId = null;
            }
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await LoadConversationsAsync();

            if (requestedMode == "chat" && requestedMissionId.HasValue)
            {
                await OpenConversationAsync(requestedMissionId.Value);
            }
            else
            {
                ShowConversationList();
            }
        }
        catch (Exception)
        {
            ShowConversationList();
            ShowError("Impossible de charger vos messages pour le moment. Réessayez dans quelques instants.");
        }
    }

    private async Task LoadConversationsAsync()
    {
        ErrorLabel.IsVisible = false;
        conversations.Clear();

        if (!sessionStore.HasSession())
        {
            ShowError("Connectez-vous pour consulter vos conversations.");
            return;
        }

        if (sessionStore.IsPreviewMode())
        {
            conversations.Add(new ConversationRow(
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
            conversations.Add(ConversationRow.From(mission));
        }
    }

    private async void OnConversationSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ConversationRow conversation)
        {
            return;
        }

        ConversationsView.SelectedItem = null;
        await OpenConversationAsync(conversation.MissionId);
    }

    private async Task OpenConversationAsync(Guid missionId)
    {
        activeMissionId = missionId;
        requestedMissionId = missionId;
        requestedMode = "chat";
        ShowChat();
        await LoadMessagesAsync(missionId);
    }

    private async Task LoadMessagesAsync(Guid missionId)
    {
        ErrorLabel.IsVisible = false;
        messages.Clear();

        if (sessionStore.IsPreviewMode())
        {
            MissionContextLabel.Text = "Mission WL-000145 · Déboucher un évier";
            messages.Add(new MessageRow("Mohamed Kouyaté", "Bonjour, j'arrive dans 13 min.", "10:45"));
            messages.Add(new MessageRow("Vous", "Parfait, je suis là.", "10:50"));
            messages.Add(new MessageRow("Support Wélé", "La mission est suivie par notre équipe.", "10:52"));
            return;
        }

        var result = await apiClient.GetMissionMessagesAsync(missionId);
        if (!result.IsSuccess || result.Response is null)
        {
            ShowError(result.ErrorMessage ?? "Impossible de charger cette conversation.");
            return;
        }

        MissionContextLabel.Text = $"Mission {result.Response.MissionNumber} · {result.Response.MissionLabel}";
        foreach (var message in result.Response.Messages)
        {
            messages.Add(MessageRow.From(message));
        }

        if (messages.Count == 0)
        {
            messages.Add(new MessageRow("Wélé", "Aucun message pour le moment. Écrivez le premier message concernant cette demande.", string.Empty));
        }
    }

    private async void OnSendClicked(object sender, EventArgs e)
    {
        if (!activeMissionId.HasValue || string.IsNullOrWhiteSpace(MessageEntry.Text))
        {
            return;
        }

        ErrorLabel.IsVisible = false;
        var body = MessageEntry.Text.Trim();
        MessageEntry.Text = string.Empty;

        if (sessionStore.IsPreviewMode())
        {
            messages.Add(new MessageRow("Vous", body, DateTime.Now.ToString("HH:mm")));
            return;
        }

        SendButton.IsEnabled = false;
        var result = await apiClient.SendMissionMessageAsync(activeMissionId.Value, body);
        SendButton.IsEnabled = true;

        if (result.IsSuccess)
        {
            await LoadMessagesAsync(activeMissionId.Value);
            return;
        }

        MessageEntry.Text = body;
        ShowError(result.ErrorMessage ?? "Le message n'a pas pu être envoyé.");
    }

    private void OnBackToConversationsClicked(object sender, EventArgs e)
    {
        requestedMode = "list";
        requestedMissionId = null;
        activeMissionId = null;
        ShowConversationList();
    }

    private void ShowConversationList()
    {
        requestedMode = "list";
        requestedMissionId = null;
        activeMissionId = null;
        ConversationListPanel.IsVisible = true;
        ChatPanel.IsVisible = false;
        BackToConversationsButton.IsVisible = false;
        EyebrowLabel.Text = "MESSAGES";
        PageTitleLabel.Text = "Conversations";
        PageSubtitleLabel.Text = "Choisissez une demande pour ouvrir ses échanges.";
        ErrorLabel.IsVisible = false;
    }

    private void ShowChat()
    {
        ConversationListPanel.IsVisible = false;
        ChatPanel.IsVisible = true;
        BackToConversationsButton.IsVisible = true;
        EyebrowLabel.Text = "DISCUSSION";
        PageTitleLabel.Text = "Messages";
        PageSubtitleLabel.Text = "Échanges liés à cette demande";
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }

    private sealed record ConversationRow(Guid MissionId, string Title, string MissionNumber, string Hint, string DateLabel)
    {
        public static ConversationRow From(ClientMissionListItemResponse response)
        {
            var title = response.PrestationName ?? response.ServiceName ?? "Demande de service";
            var date = (response.ScheduledFor ?? response.CreatedAt).ToLocalTime().ToString("dd MMM · HH:mm");
            return new ConversationRow(response.MissionId, title, response.MissionNumber, "Ouvrir la conversation", date);
        }
    }

    private sealed record MessageRow(string Sender, string Body, string SentAt)
    {
        public static MessageRow From(ClientMissionMessageResponse response)
        {
            var sender = response.SenderType.Equals("Customer", StringComparison.OrdinalIgnoreCase)
                ? "Vous"
                : response.SenderType;
            return new MessageRow(sender, response.Body, response.CreatedAt.ToLocalTime().ToString("dd/MM HH:mm"));
        }
    }
}
