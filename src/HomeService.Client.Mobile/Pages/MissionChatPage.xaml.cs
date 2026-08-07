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
    private readonly SemaphoreSlim loadGate = new(1, 1);
    private CancellationTokenSource? refreshCancellation;
    private string? lastMessageSignature;
    private Guid? missionId;
    private bool isSending;
    private bool isNavigating;

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
        isNavigating = false;
        await LoadMessagesSafelyAsync();
        StartRefresh();
    }

    protected override void OnDisappearing()
    {
        refreshCancellation?.Cancel();
        refreshCancellation?.Dispose();
        refreshCancellation = null;
        base.OnDisappearing();
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        await LoadMessagesSafelyAsync();
        ChatRefreshView.IsRefreshing = false;
    }

    private async Task LoadMessagesSafelyAsync()
    {
        if (!missionId.HasValue)
        {
            ShowError("Cette conversation n'est pas liée à une demande valide.");
            return;
        }

        if (!await loadGate.WaitAsync(0))
        {
            return;
        }

        ErrorLabel.IsVisible = false;
        try
        {
            if (sessionStore.IsPreviewMode())
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                    MissionContextLabel.Text = "WL-000145 · Déboucher un évier");
                await ReplaceMessagesAsync([
                    new ClientMessageRow(Guid.NewGuid(), "Mohamed Kouyaté", "Bonjour, j'arrive dans 13 min.", "10:45"),
                    new ClientMessageRow(Guid.NewGuid(), "Vous", "Parfait, je suis là.", "10:50")]);
                return;
            }

            var result = await apiClient.GetMissionMessagesAsync(missionId.Value);
            if (!result.IsSuccess || result.Response is null)
            {
                ShowError(result.ErrorMessage ?? "Impossible de charger cette conversation.");
                return;
            }

            var rows = (result.Response.Messages ?? [])
                .Select(ClientMessageRow.From)
                .ToArray();
            await MainThread.InvokeOnMainThreadAsync(() =>
                MissionContextLabel.Text = $"{result.Response.MissionNumber} · {result.Response.MissionLabel}");
            await ReplaceMessagesAsync(rows);
            if (Shell.Current is AppShell shell) _ = shell.RefreshNavigationBadgesAsync();
        }
        catch
        {
            ShowError("Impossible de charger cette conversation. Vérifiez votre connexion puis réessayez.");
        }
        finally
        {
            loadGate.Release();
        }
    }

    private async void OnSendClicked(object sender, EventArgs e) => await SendAsync();

    private async void OnMessageCompleted(object sender, EventArgs e) => await SendAsync();

    private async Task SendAsync()
    {
        if (!missionId.HasValue || string.IsNullOrWhiteSpace(MessageEntry.Text) || isSending)
        {
            return;
        }

        isSending = true;
        var body = MessageEntry.Text.Trim();
        ErrorLabel.IsVisible = false;
        SendButton.IsEnabled = false;
        try
        {
            if (sessionStore.IsPreviewMode())
            {
                messages.Add(new ClientMessageRow(Guid.NewGuid(), "Vous", body, DateTime.Now.ToString("HH:mm")));
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
            await LoadMessagesSafelyAsync();
        }
        catch
        {
            ShowError("Le message n'a pas pu être envoyé. Vérifiez votre connexion puis réessayez.");
        }
        finally
        {
            isSending = false;
            SendButton.IsEnabled = true;
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        if (isNavigating)
        {
            return;
        }

        isNavigating = true;
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

    private Task ReplaceMessagesAsync(IEnumerable<ClientMessageRow> rows)
    {
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            var next = rows.ToArray();
            var signature = string.Join('|', next.Select(item => item.MessageId));
            if (signature == lastMessageSignature) return;
            lastMessageSignature = signature;
            messages.Clear();
            foreach (var row in next)
            {
                messages.Add(row);
            }
            if (messages.Count > 0)
            {
                MessagesView.ScrollTo(messages[^1], position: ScrollToPosition.End, animate: false);
            }
        });
    }

    private void StartRefresh()
    {
        if (sessionStore.IsPreviewMode()) return;
        refreshCancellation?.Cancel();
        refreshCancellation?.Dispose();
        refreshCancellation = new CancellationTokenSource();
        _ = RefreshLoopAsync(refreshCancellation.Token);
    }

    private async Task RefreshLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(4));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await LoadMessagesSafelyAsync();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }
}

public sealed record ClientMessageRow(Guid MessageId, string Sender, string Body, string SentAt)
{
    public bool IsMine => Sender.Equals("Vous", StringComparison.OrdinalIgnoreCase);

    public static ClientMessageRow From(ClientMissionMessageResponse response)
    {
        var sender = response.SenderType switch
        {
            "Customer" => "Vous",
            "Provider" => "Prestataire",
            "Company" => "Entreprise",
            _ => "Wélé"
        };
        return new ClientMessageRow(response.MessageId, sender, response.Body, response.CreatedAt.ToLocalTime().ToString("dd/MM HH:mm"));
    }
}
