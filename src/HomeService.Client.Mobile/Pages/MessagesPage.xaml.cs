using System.Collections.ObjectModel;
using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

public partial class MessagesPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient;
    private readonly ClientSessionStore sessionStore;
    private readonly ObservableCollection<MessageRow> messages = [];
    private readonly List<MissionChoice> missions = [];

    public MessagesPage()
    {
        InitializeComponent();
        apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
        sessionStore = MobileServiceLocator.GetRequiredService<ClientSessionStore>();
        MessagesView.ItemsSource = messages;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadMissionsAsync();
    }

    private async Task LoadMissionsAsync()
    {
        messages.Clear();
        missions.Clear();
        MissionPicker.ItemsSource = null;

        if (!sessionStore.HasSession())
        {
            messages.Add(new MessageRow("Systeme", "Connectez-vous pour consulter vos conversations.", string.Empty));
            return;
        }

        var result = await apiClient.GetMissionsAsync();
        if (!result.IsSuccess || result.Response is null || result.Response.Count == 0)
        {
            messages.Add(new MessageRow("Systeme", "Aucune conversation disponible pour le moment.", string.Empty));
            return;
        }

        missions.AddRange(result.Response.Take(12).Select(MissionChoice.From));
        MissionPicker.ItemsSource = missions;
        MissionPicker.SelectedIndex = 0;
    }

    private async void OnMissionChanged(object sender, EventArgs e)
    {
        if (MissionPicker.SelectedItem is MissionChoice mission)
        {
            await LoadMessagesAsync(mission.MissionId);
        }
    }

    private async Task LoadMessagesAsync(Guid missionId)
    {
        messages.Clear();
        var result = await apiClient.GetMissionMessagesAsync(missionId);
        if (!result.IsSuccess || result.Response is null || result.Response.Messages.Count == 0)
        {
            messages.Add(new MessageRow("Systeme", "Aucun message sur cette mission.", string.Empty));
            return;
        }

        foreach (var message in result.Response.Messages)
        {
            messages.Add(MessageRow.From(message));
        }
    }

    private async void OnSendClicked(object sender, EventArgs e)
    {
        if (MissionPicker.SelectedItem is not MissionChoice mission || string.IsNullOrWhiteSpace(MessageEntry.Text))
        {
            return;
        }

        var body = MessageEntry.Text.Trim();
        MessageEntry.Text = string.Empty;
        var result = await apiClient.SendMissionMessageAsync(mission.MissionId, body);
        if (result.IsSuccess)
        {
            await LoadMessagesAsync(mission.MissionId);
        }
    }

    private sealed record MissionChoice(Guid MissionId, string Label)
    {
        public static MissionChoice From(ClientMissionListItemResponse response)
        {
            return new MissionChoice(response.MissionId, $"{response.MissionNumber} - {response.PrestationName ?? response.ServiceName}");
        }
    }

    private sealed record MessageRow(string Sender, string Body, string SentAt)
    {
        public static MessageRow From(ClientMissionMessageResponse response)
        {
            return new MessageRow(response.SenderType, response.Body, response.CreatedAt.ToString("dd/MM HH:mm"));
        }
    }
}
