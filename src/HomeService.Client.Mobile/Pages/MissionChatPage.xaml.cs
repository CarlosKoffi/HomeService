using System.Collections.ObjectModel;
using HomeService.Client.Mobile.Services;
using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Pages;

[QueryProperty(nameof(MissionId), "missionId")]
public partial class MissionChatPage : ContentPage
{
    private readonly ClientMobileApiClient apiClient;
    private readonly ClientSessionStore sessionStore;
    private readonly ObservableCollection<ClientMessageRow> messages = [];
    private Guid? missionId;
    private bool isLoading;

    public MissionChatPage()
    {
        InitializeComponent();
        apiClient = MobileServiceLocator.GetRequiredService<ClientMobileApiClient>();
        sessionStore = MobileServiceLocator.GetRequiredService<ClientSessionStore>();
        MessagesView.ItemsSource = messages;
    }

    public string? MissionId
    {
        set => missionId = Guid.TryParse(value, out var parsed) ? parsed : null;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadMessagesSafelyAsync();
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        await LoadMessagesSafelyAsync(force: true);
        ChatRefreshView.IsRefreshing = false;
    }

    private async Task LoadMessagesSafelyAsync(bool force = false)
    {
        if ((isLoading && !force) || !missionId.HasValue)
        {
            if (!missionId.HasValue)
            {
                ShowError("Cette conversation n'est pas liée à une demande valide.");
            }
            return;
        }

        isLoading = true;
        ErrorLabel.IsVisible = false;
        try
        {
            messages.Clear();
            if (sessionStore.IsPreviewMode())
            {
                MissionContextLabel.Text = "WL-000145 · Déboucher un évier";
                messages.Add(new ClientMessageRow("Mohamed Kouyaté", "Bonjour, j'arrive dans 13 min.", "10:45"));
                messages.Add(new ClientMessageRow("Vous", "Parfait, je suis là.", "10:50"));
                return;
            }

            var result = await apiClient.GetMissionMessagesAsync(missionId.Value);
            if (!result.IsSuccess || result.Response is null)
            {
                ShowError(result.ErrorMessage ?? "Impossible de charger cette conversation.");
                return;
            }

            MissionContextLabel.Text = $"{result.Response.MissionNumber} · {result.Response.MissionLabel}";
            foreach (var message in result.Response.Messages)
            {
                messages.Add(ClientMessageRow.From(message));
            }
        }
        catch
        {
            ShowError("Impossible de charger cette conversation. Vérifiez votre connexion puis réessayez.");
        }
        finally
        {
            isLoading = false;
        }
    }

    private async void OnSendClicked(object sender, EventArgs e) => await SendAsync();

    private async void OnMessageCompleted(object sender, EventArgs e) => await SendAsync();

    private async Task SendAsync()
    {
        if (!missionId.HasValue || string.IsNullOrWhiteSpace(MessageEntry.Text) || !SendButton.IsEnabled)
        {
            return;
        }

        var body = MessageEntry.Text.Trim();
        ErrorLabel.IsVisible = false;
        SendButton.IsEnabled = false;
        try
        {
            if (sessionStore.IsPreviewMode())
            {
                messages.Add(new ClientMessageRow("Vous", body, DateTime.Now.ToString("HH:mm")));
                MessageEntry.Text = string.Empty;
                return;
            }

            var result = await apiClient.SendMissionMessageAsync(missionId.Value, body);
            if (!result.IsSuccess)
            {
                ShowError(result.ErrorMessage ?? "Le message n'a pas pu être envoyé.");
                return;
            }

            MessageEntry.Text = string.Empty;
            await LoadMessagesSafelyAsync(force: true);
        }
        catch
        {
            ShowError("Le message n'a pas pu être envoyé. Vérifiez votre connexion puis réessayez.");
        }
        finally
        {
            SendButton.IsEnabled = true;
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync("..");
        }
        catch
        {
            await Shell.Current.GoToAsync("//messages");
        }
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}

public sealed record ClientMessageRow(string Sender, string Body, string SentAt)
{
    public static ClientMessageRow From(ClientMissionMessageResponse response)
    {
        var sender = response.SenderType.Equals("Customer", StringComparison.OrdinalIgnoreCase)
            ? "Vous"
            : response.SenderType;
        return new ClientMessageRow(sender, response.Body, response.CreatedAt.ToLocalTime().ToString("dd/MM HH:mm"));
    }
}
