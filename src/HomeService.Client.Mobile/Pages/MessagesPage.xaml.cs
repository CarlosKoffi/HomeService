using System.Collections.ObjectModel;
using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

[QueryProperty(nameof(MissionId), "missionId")]
public partial class MessagesPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient;
    private readonly ClientSessionStore sessionStore;
    private readonly ObservableCollection<MessageRow> messages = [];
    private readonly List<MissionChoice> missions = [];
    private Guid? requestedMissionId;

    public MessagesPage()
    {
        InitializeComponent();
        apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
        sessionStore = MobileServiceLocator.GetRequiredService<ClientSessionStore>();
        MessagesView.ItemsSource = messages;
    }

    public string? MissionId
    {
        set
        {
            requestedMissionId = Guid.TryParse(value, out var missionId)
                ? missionId
                : null;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadMissionsAsync();
    }

    private async Task LoadMissionsAsync()
    {
        ErrorLabel.IsVisible = false;
        messages.Clear();
        missions.Clear();
        MissionPicker.ItemsSource = null;

        if (!sessionStore.HasSession())
        {
            messages.Add(new MessageRow("Systeme", "Connectez-vous pour consulter vos conversations.", string.Empty));
            return;
        }

        if (sessionStore.IsPreviewMode())
        {
            missions.Add(new MissionChoice(Guid.Parse("11111111-1111-1111-1111-111111111111"), "WL-000145 - Déboucher un évier"));
            MissionPicker.ItemsSource = missions;
            MissionPicker.SelectedIndex = 0;
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

        var selectedMission = requestedMissionId.HasValue
            ? missions.FindIndex(mission => mission.MissionId == requestedMissionId.Value)
            : -1;

        MissionPicker.SelectedIndex = selectedMission >= 0 ? selectedMission : 0;
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
        ErrorLabel.IsVisible = false;
        messages.Clear();
        if (sessionStore.IsPreviewMode())
        {
            MissionContextLabel.Text = "Mission WL-000145 - Deboucher un evier";
            messages.Add(new MessageRow("Mohamed Kouyaté", "Bonjour, j'arrive dans 13 min.", "10:45"));
            messages.Add(new MessageRow("Vous", "Parfait, je suis là.", "10:50"));
            messages.Add(new MessageRow("Support Wélé", "La mission est suivie par notre équipe.", "10:52"));
            return;
        }

        var result = await apiClient.GetMissionMessagesAsync(missionId);
        if (!result.IsSuccess || result.Response is null)
        {
            ShowError(result.ErrorMessage);
            messages.Add(new MessageRow("Systeme", "Impossible de charger cette conversation.", string.Empty));
            return;
        }

        MissionContextLabel.Text = $"Mission {result.Response.MissionNumber} - {result.Response.MissionLabel}";

        if (result.Response.Messages.Count == 0)
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

        ErrorLabel.IsVisible = false;
        var body = MessageEntry.Text.Trim();
        MessageEntry.Text = string.Empty;
        if (sessionStore.IsPreviewMode())
        {
            messages.Add(new MessageRow("Vous", body, DateTime.Now.ToString("HH:mm")));
            return;
        }

        SendButton.IsEnabled = false;
        var result = await apiClient.SendMissionMessageAsync(mission.MissionId, body);
        SendButton.IsEnabled = true;

        if (result.IsSuccess)
        {
            await LoadMessagesAsync(mission.MissionId);
            return;
        }

        MessageEntry.Text = body;
        ShowError(result.ErrorMessage);
    }

    private void ShowError(string? message)
    {
        ErrorLabel.Text = message ?? "Action impossible.";
        ErrorLabel.IsVisible = true;
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
