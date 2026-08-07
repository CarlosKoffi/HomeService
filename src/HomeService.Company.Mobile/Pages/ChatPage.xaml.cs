using System.Collections.ObjectModel;
using HomeService.Company.Mobile.Services;
using HomeService.Contracts.CompanyPortal;

namespace HomeService.Company.Mobile.Pages;

[QueryProperty(nameof(MissionId), "missionId")]
public partial class ChatPage : ContentPage
{
    private static readonly TimeSpan ChatRefreshInterval = TimeSpan.FromSeconds(4);
    private readonly CompanyMobileApiClient apiClient;
    private readonly CompanySessionStore sessionStore;
    private readonly ObservableCollection<CompanyChatMessageRow> messages = [];
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private CancellationTokenSource? refreshCancellation;
    private Guid? requestedMissionId;
    private Guid? selectedMissionId;
    private IReadOnlyList<CompanyChatMissionRow> missions = [];
    private string? lastMessageSignature;
    private bool selecting;
    private bool sending;

    public string? MissionId
    {
        set => requestedMissionId = Guid.TryParse(value, out var parsed) ? parsed : null;
    }

    public ChatPage()
    {
        InitializeComponent();
        apiClient = IPlatformApplication.Current!.Services.GetRequiredService<CompanyMobileApiClient>();
        sessionStore = IPlatformApplication.Current.Services.GetRequiredService<CompanySessionStore>();
        MessagesView.ItemsSource = messages;
        SetComposerEnabled(false);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadMissionsAsync();
        StartRefresh();
    }

    protected override void OnDisappearing()
    {
        refreshCancellation?.Cancel();
        refreshCancellation?.Dispose();
        refreshCancellation = null;
        base.OnDisappearing();
    }

    private async Task LoadMissionsAsync()
    {
        var token = await sessionStore.GetTokenAsync();
        var companyId = await sessionStore.GetCompanyIdAsync();
        if (string.IsNullOrWhiteSpace(token) || !companyId.HasValue) return;
        var result = await apiClient.GetMissionsAsync(token, companyId.Value);
        if (!result.IsSuccess)
        {
            ShowError(result.ErrorMessage ?? "Impossible de charger les conversations.");
            return;
        }

        missions = (result.Response ?? [])
            .Where(item => item.Status is not "Cancelled")
            .OrderBy(item => StatusOrder(item.Status))
            .ThenByDescending(item => item.ScheduledFor ?? DateTimeOffset.MinValue)
            .Take(30)
            .Select(item => new CompanyChatMissionRow(
                item.Id,
                item.Status,
                $"{item.ServiceName} · {item.CustomerName} · {item.MissionNumber}"))
            .ToList();
        MissionPicker.ItemsSource = missions.ToList();

        var target = requestedMissionId.HasValue && missions.Any(item => item.MissionId == requestedMissionId)
            ? requestedMissionId
            : selectedMissionId.HasValue && missions.Any(item => item.MissionId == selectedMissionId)
                ? selectedMissionId
                : missions.FirstOrDefault()?.MissionId;
        requestedMissionId = null;
        if (target.HasValue)
        {
            MissionPicker.SelectedItem = missions.First(item => item.MissionId == target.Value);
            await SelectMissionAsync(target.Value, force: true);
        }
        else
        {
            messages.Clear();
            RecipientLabel.Text = "Aucune conversation disponible";
            SetComposerEnabled(false);
        }
    }

    private async void OnMissionSelected(object? sender, EventArgs e)
    {
        if (selecting || MissionPicker.SelectedItem is not CompanyChatMissionRow row) return;
        await SelectMissionAsync(row.MissionId, force: true);
    }

    private async Task SelectMissionAsync(Guid missionId, bool force)
    {
        if (!await refreshGate.WaitAsync(0)) return;
        try
        {
            selecting = true;
            selectedMissionId = missionId;
            var token = await sessionStore.GetTokenAsync();
            var companyId = await sessionStore.GetCompanyIdAsync();
            if (string.IsNullOrWhiteSpace(token) || !companyId.HasValue) return;
            var result = await apiClient.GetMissionMessagesAsync(token, companyId.Value, missionId);
            if (!result.IsSuccess || result.Response is null)
            {
                if (force) ShowError(result.ErrorMessage ?? "Impossible de charger cette conversation.");
                return;
            }

            var response = result.Response;
            RecipientLabel.Text = response.ProviderName is null
                ? $"Avec {response.CustomerName}"
                : $"Avec {response.CustomerName} et {response.ProviderName}";
            var signature = string.Join('|', response.Messages.Select(item => item.MessageId));
            if (force || signature != lastMessageSignature)
            {
                lastMessageSignature = signature;
                messages.Clear();
                foreach (var message in response.Messages.OrderBy(item => item.CreatedAt))
                {
                    messages.Add(CompanyChatMessageRow.From(message));
                }

                if (messages.Count > 0)
                {
                    await Task.Delay(30);
                    MessagesView.ScrollTo(messages[^1], position: ScrollToPosition.End, animate: false);
                }
            }

            var mission = missions.FirstOrDefault(item => item.MissionId == missionId);
            SetComposerEnabled(mission?.Status is not ("Completed" or "Cancelled" or "Resolved"));
            ErrorLabel.IsVisible = false;
            if (Shell.Current is AppShell shell) _ = shell.RefreshNavigationBadgesAsync();
        }
        finally
        {
            selecting = false;
            refreshGate.Release();
        }
    }

    private void StartRefresh()
    {
        refreshCancellation?.Cancel();
        refreshCancellation?.Dispose();
        refreshCancellation = new CancellationTokenSource();
        _ = RefreshLoopAsync(refreshCancellation.Token);
    }

    private async Task RefreshLoopAsync(CancellationToken cancellationToken)
    {
        var cycle = 0;
        try
        {
            using var timer = new PeriodicTimer(ChatRefreshInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                cycle++;
                if (selectedMissionId.HasValue)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => SelectMissionAsync(selectedMissionId.Value, force: false));
                }
                if (cycle % 4 == 0)
                {
                    await MainThread.InvokeOnMainThreadAsync(LoadMissionsAsync);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private async void OnSendClicked(object? sender, EventArgs e) => await SendAsync();
    private async void OnMessageCompleted(object? sender, EventArgs e) => await SendAsync();

    private async Task SendAsync()
    {
        var body = MessageEntry.Text?.Trim();
        if (sending || !selectedMissionId.HasValue || string.IsNullOrWhiteSpace(body)) return;
        sending = true;
        SetComposerEnabled(false);
        try
        {
            var token = await sessionStore.GetTokenAsync();
            var companyId = await sessionStore.GetCompanyIdAsync();
            if (string.IsNullOrWhiteSpace(token) || !companyId.HasValue) return;
            var result = await apiClient.SendMissionMessageAsync(
                token,
                companyId.Value,
                selectedMissionId.Value,
                new SendCompanyMissionMessageRequest(body));
            if (!result.IsSuccess)
            {
                ShowError(result.ErrorMessage ?? "Message non envoyé.");
                return;
            }

            MessageEntry.Text = string.Empty;
            await SelectMissionAsync(selectedMissionId.Value, force: true);
        }
        finally
        {
            sending = false;
            var mission = missions.FirstOrDefault(item => item.MissionId == selectedMissionId);
            SetComposerEnabled(mission?.Status is not ("Completed" or "Cancelled" or "Resolved"));
        }
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        if (selectedMissionId.HasValue) await SelectMissionAsync(selectedMissionId.Value, force: true);
        ChatRefreshView.IsRefreshing = false;
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await NavigateBackSafelyAsync();

    protected override bool OnBackButtonPressed()
    {
        MainThread.BeginInvokeOnMainThread(async () => await NavigateBackSafelyAsync());
        return true;
    }

    private static async Task NavigateBackSafelyAsync()
    {
        try
        {
            if (Shell.Current.Navigation.NavigationStack.Count > 1)
            {
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await Shell.Current.GoToAsync("//home");
            }
        }
        catch (InvalidOperationException)
        {
            await Shell.Current.GoToAsync("//home");
        }
    }

    private void SetComposerEnabled(bool enabled)
    {
        MessageEntry.IsEnabled = enabled;
        SendButton.IsEnabled = enabled;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }

    private static int StatusOrder(string status) => status switch
    {
        "Started" => 0,
        "OnTheWay" => 1,
        "Accepted" => 2,
        "Assigned" or "Offered" or "SearchingProvider" => 3,
        _ => 4
    };
}

public sealed record CompanyChatMissionRow(Guid MissionId, string Status, string Label);

public sealed record CompanyChatMessageRow(
    Guid MessageId,
    string SenderLabel,
    string Body,
    Color BubbleColor,
    Color StrokeColor,
    Color TextColor,
    Color MetaColor,
    LayoutOptions Alignment)
{
    public static CompanyChatMessageRow From(CompanyMissionMessageResponse response)
    {
        var mine = response.SenderType.Equals("Company", StringComparison.OrdinalIgnoreCase);
        var sender = response.SenderType switch
        {
            "Company" => "Vous",
            "Customer" => "Client",
            "Provider" => "Prestataire",
            _ => "Wélé"
        };
        return new CompanyChatMessageRow(
            response.MessageId,
            $"{sender} · {response.CreatedAt.LocalDateTime:HH:mm}",
            response.Body,
            Color.FromArgb(mine ? "#155EEF" : "#F8FAFC"),
            Color.FromArgb(mine ? "#155EEF" : "#E6E9EF"),
            Color.FromArgb(mine ? "#FFFFFF" : "#0F172A"),
            Color.FromArgb(mine ? "#DDE8FF" : "#667085"),
            mine ? LayoutOptions.End : LayoutOptions.Start);
    }
}
