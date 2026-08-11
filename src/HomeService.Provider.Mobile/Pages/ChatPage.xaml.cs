using HomeService.Contracts.ProviderPortal;
using HomeService.Provider.Mobile.Services;
using HomeService.Mobile.Shared;
using Microsoft.Maui.Controls.Shapes;

namespace HomeService.Provider.Mobile.Pages;

[QueryProperty(nameof(AssignmentId), "assignmentId")]
public partial class ChatPage : ContentPage
{
    private readonly ProviderMobileApiClient? apiClient;
    private readonly ProviderSessionService? sessionService;
    private readonly CatalogMediaResolver? catalogMedia;
    private readonly Dictionary<Guid, Border> conversationCards = [];
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private string? accessToken;
    private Guid? requestedAssignmentId;
    private Guid? assignmentId;
    private IReadOnlyList<ProviderMobileMissionSummaryResponse> missions = [];
    private CancellationTokenSource? refreshCancellation;
    private string? lastMessageSignature;

    public string AssignmentId
    {
        set
        {
            if (Guid.TryParse(value, out var parsed)) requestedAssignmentId = parsed;
        }
    }

    public ChatPage()
    {
        InitializeComponent();
        apiClient = IPlatformApplication.Current?.Services.GetService<ProviderMobileApiClient>();
        sessionService = IPlatformApplication.Current?.Services.GetService<ProviderSessionService>();
        catalogMedia = IPlatformApplication.Current?.Services.GetService<CatalogMediaResolver>();
        SetComposerEnabled(false);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadConversationsAsync();
        StartRefresh();
    }

    protected override void OnDisappearing()
    {
        refreshCancellation?.Cancel();
        refreshCancellation?.Dispose();
        refreshCancellation = null;
        base.OnDisappearing();
    }

    private async Task LoadConversationsAsync()
    {
        MessageBanner.IsVisible = false;
        if (apiClient is null || sessionService is null)
        {
            ShowMessage("Configuration mobile incomplète. Client API introuvable.");
            await RenderConversationChoicesAsync([]);
            return;
        }

        accessToken = await sessionService.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            ShowMessage("Connectez-vous pour consulter vos messages.");
            await RenderConversationChoicesAsync([]);
            return;
        }

        var result = await apiClient.GetMissionsAsync(accessToken);
        if (!result.IsSuccess || result.Response is null)
        {
            ShowMessage(result.ErrorMessage ?? "Impossible de charger les conversations.");
            await RenderConversationChoicesAsync([]);
            return;
        }

        missions = result.Response.Items
            .Where(item => IsConversationActive(item.Status))
            .OrderBy(item => StatusOrder(item.Status))
            .ThenByDescending(item => item.ScheduledFor ?? DateTimeOffset.MinValue)
            .Take(20)
            .ToList();
        await RenderConversationChoicesAsync(missions);

        var selectedId = requestedAssignmentId is not null && missions.Any(item => item.AssignmentId == requestedAssignmentId)
            ? requestedAssignmentId
            : assignmentId is not null && missions.Any(item => item.AssignmentId == assignmentId)
                ? assignmentId
                : missions.FirstOrDefault()?.AssignmentId;
        requestedAssignmentId = null;
        if (selectedId is not null) await SelectConversationAsync(selectedId.Value);
        else RenderNoConversation();
    }

    private async Task RenderConversationChoicesAsync(IReadOnlyList<ProviderMobileMissionSummaryResponse> items)
    {
        ConversationListStack.Children.Clear();
        conversationCards.Clear();
        if (items.Count == 0)
        {
            ConversationListStack.Add(new Label { Text = "Aucune conversation", FontFamily = "PlusJakartaSans", FontSize = 13, TextColor = Color.FromArgb("#667085") });
            return;
        }

        foreach (var mission in items)
        {
            var title = string.IsNullOrWhiteSpace(mission.PrestationName) ? mission.ServiceName : mission.PrestationName;
            var grid = new Grid { ColumnDefinitions = Columns(GridLength.Auto, GridLength.Star, GridLength.Auto), ColumnSpacing = 8 };
            var serviceImage = new Image { Source = ProviderIconResolver.ForService(mission.ServiceIconName, mission.ServiceName), WidthRequest = 30, HeightRequest = 30, Aspect = Aspect.AspectFit };
            if (catalogMedia is not null)
            {
                var remote = string.IsNullOrWhiteSpace(mission.PrestationName)
                    ? await catalogMedia.ResolveServiceAsync(null, mission.ServiceName)
                    : await catalogMedia.ResolvePrestationAsync(null, mission.PrestationName, serviceName: mission.ServiceName);
                if (remote is not null) serviceImage.Source = remote;
            }
            grid.Add(serviceImage, 0);
            grid.Add(new VerticalStackLayout
            {
                Spacing = 1,
                Children =
                {
                    new Label { Text = title, FontFamily = "PlusJakartaSans", FontSize = 12, FontAttributes = FontAttributes.Bold, MaxLines = 1 },
                    new Label { Text = mission.MissionNumber, FontFamily = "PlusJakartaSans", FontSize = 9, TextColor = Color.FromArgb("#667085") }
                }
            }, 1);
            if (mission.UnreadMessageCount > 0)
            {
                grid.Add(new Border
                {
                    BackgroundColor = Color.FromArgb("#155EEF"),
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 10 },
                    Padding = new Thickness(7, 3),
                    VerticalOptions = LayoutOptions.Center,
                    Content = new Label
                    {
                        Text = mission.UnreadMessageCount.ToString(),
                        FontFamily = "PlusJakartaSans",
                        FontSize = 9,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Colors.White
                    }
                }, 2);
            }
            var card = new Border
            {
                BackgroundColor = Colors.White,
                Stroke = Color.FromArgb("#DCE8FF"),
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Padding = new Thickness(11, 8),
                Content = grid,
                MaximumWidthRequest = 210
            };
            var tap = new TapGestureRecognizer { CommandParameter = mission.AssignmentId };
            tap.Tapped += OnConversationTapped;
            card.GestureRecognizers.Add(tap);
            ConversationListStack.Add(card);
            conversationCards[mission.AssignmentId] = card;
        }
    }

    private async void OnConversationTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is Guid id) await SelectConversationAsync(id);
    }

    private async Task SelectConversationAsync(Guid id)
    {
        if (apiClient is null || string.IsNullOrWhiteSpace(accessToken)) return;
        assignmentId = id;
        UpdateSelectedCard();
        SetComposerEnabled(false);
        MessagesStack.Children.Clear();
        MessagesStack.Add(new ActivityIndicator { IsRunning = true, Color = Color.FromArgb("#155EEF") });

        var detailTask = apiClient.GetMissionDetailAsync(accessToken, id);
        var chatTask = apiClient.GetMissionMessagesAsync(accessToken, id);
        await Task.WhenAll(detailTask, chatTask);
        var detailResult = await detailTask;
        var chatResult = await chatTask;
        if (!chatResult.IsSuccess || chatResult.Response is null)
        {
            if (chatResult.StatusCode is 400 or 404)
            {
                await LoadConversationsAsync();
                return;
            }

            ShowMessage(chatResult.ErrorMessage ?? "Impossible de charger cette conversation.");
            MessagesStack.Children.Clear();
            return;
        }

        var mission = missions.First(item => item.AssignmentId == id);
        var customer = detailResult.Response?.CustomerDisplayName;
        RecipientCard.IsVisible = true;
        RecipientTitleLabel.Text = string.IsNullOrWhiteSpace(customer) ? "Vous écrivez au client" : $"Vous écrivez à {customer}";
        RecipientSubtitleLabel.Text = $"Mission {mission.MissionNumber} · {(string.IsNullOrWhiteSpace(mission.PrestationName) ? mission.ServiceName : mission.PrestationName)}";
        MessageEntry.Placeholder = string.IsNullOrWhiteSpace(customer) ? "Écrire au client…" : $"Écrire à {FirstName(customer)}…";
        MessageBanner.IsVisible = false;
        lastMessageSignature = BuildMessageSignature(chatResult.Response.Messages);
        RenderMessages(chatResult.Response.Messages);
        if (Shell.Current is AppShell shell) _ = shell.RefreshNavigationBadgesAsync();
        SetComposerEnabled(IsConversationActive(mission.Status));
        await Task.Delay(50);
        await MessagesScroll.ScrollToAsync(MessagesStack, ScrollToPosition.End, false);
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
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(4));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await RefreshSelectedConversationAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private async Task RefreshSelectedConversationAsync(CancellationToken cancellationToken)
    {
        if (apiClient is null || assignmentId is null || string.IsNullOrWhiteSpace(accessToken)) return;
        if (!await refreshGate.WaitAsync(0, cancellationToken)) return;
        try
        {
            var result = await apiClient.GetMissionMessagesAsync(accessToken, assignmentId.Value, cancellationToken);
            if (!result.IsSuccess || result.Response is null)
            {
                if (result.StatusCode is 400 or 404)
                {
                    await MainThread.InvokeOnMainThreadAsync(LoadConversationsAsync);
                }
                return;
            }
            var signature = BuildMessageSignature(result.Response.Messages);
            if (signature == lastMessageSignature) return;
            lastMessageSignature = signature;
            if (Shell.Current is AppShell shell) _ = shell.RefreshNavigationBadgesAsync();
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                RenderMessages(result.Response.Messages);
                await Task.Delay(30);
                await MessagesScroll.ScrollToAsync(MessagesStack, ScrollToPosition.End, false);
            });
        }
        finally
        {
            refreshGate.Release();
        }
    }

    private void RenderMessages(IReadOnlyList<ProviderMobileMissionMessageResponse> items)
    {
        MessagesStack.Children.Clear();
        if (items.Count == 0)
        {
            MessagesStack.Add(new Border
            {
                Style = (Style)Application.Current!.Resources["PremiumCard"],
                Content = new VerticalStackLayout
                {
                    Spacing = 7,
                    Children =
                    {
                        new Image { Source = "icon_message.svg", WidthRequest = 38, HeightRequest = 38, HorizontalOptions = LayoutOptions.Center },
                        new Label { Text = "Aucun message pour le moment", FontFamily = "PlusJakartaSans", FontSize = 14, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center },
                        new Label { Text = "Utilisez cette conversation uniquement pour cette mission.", FontFamily = "PlusJakartaSans", FontSize = 12, TextColor = Color.FromArgb("#667085"), HorizontalTextAlignment = TextAlignment.Center }
                    }
                }
            });
            return;
        }

        foreach (var message in items.OrderBy(item => item.CreatedAt))
        {
            var mine = message.SenderType.Equals("Provider", StringComparison.OrdinalIgnoreCase);
            var bubbleContent = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new Label { Text = SenderLabel(message), FontFamily = "PlusJakartaSans", FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(mine ? "#DDE8FF" : "#667085") },
                    new Label { Text = message.Body, FontFamily = "PlusJakartaSans", FontSize = 14, TextColor = Color.FromArgb(mine ? "#FFFFFF" : "#0F172A"), LineHeight = 1.2 }
                }
            };
            if (mine)
            {
                bubbleContent.Children.Add(new Label
                {
                    Text = message.ReadAt is null ? "Envoyé" : "Lu",
                    FontFamily = "PlusJakartaSans",
                    FontSize = 9,
                    TextColor = Color.FromArgb("#DDE8FF"),
                    HorizontalTextAlignment = TextAlignment.End
                });
            }

            var bubble = new Border
            {
                BackgroundColor = Color.FromArgb(mine ? "#155EEF" : "#F8FAFC"),
                Stroke = Color.FromArgb(mine ? "#155EEF" : "#E6E9EF"),
                StrokeShape = new RoundRectangle { CornerRadius = 18 },
                Padding = new Thickness(13, 10),
                HorizontalOptions = mine ? LayoutOptions.End : LayoutOptions.Start,
                MaximumWidthRequest = 310,
                Content = bubbleContent
            };
            MessagesStack.Add(bubble);
        }
    }

    private async void OnSendClicked(object? sender, EventArgs e)
    {
        var body = MessageEntry.Text?.Trim();
        if (apiClient is null || assignmentId is null || string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(body)) return;
        SetComposerEnabled(false);
        var result = await apiClient.SendMissionMessageAsync(accessToken, assignmentId.Value, new SendProviderMissionMessageRequest(body));
        if (!result.IsSuccess)
        {
            ShowMessage(result.ErrorMessage ?? "Message non envoyé.");
            SetComposerEnabled(true);
            return;
        }

        MessageEntry.Text = string.Empty;
        await SelectConversationAsync(assignmentId.Value);
    }

    private void UpdateSelectedCard()
    {
        foreach (var pair in conversationCards)
        {
            var selected = pair.Key == assignmentId;
            pair.Value.BackgroundColor = Color.FromArgb(selected ? "#EEF4FF" : "#FFFFFF");
            pair.Value.Stroke = Color.FromArgb(selected ? "#155EEF" : "#DCE8FF");
        }
    }

    private void RenderNoConversation()
    {
        assignmentId = null;
        RecipientCard.IsVisible = false;
        MessagesStack.Children.Clear();
        MessagesStack.Add(new Label { Text = "Une conversation apparaîtra dès qu’une mission vous sera affectée.", FontFamily = "PlusJakartaSans", FontSize = 13, TextColor = Color.FromArgb("#667085"), HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(20) });
        MessageEntry.Placeholder = "Choisissez d’abord une mission";
        SetComposerEnabled(false);
    }

    private void SetComposerEnabled(bool enabled)
    {
        MessageEntry.IsEnabled = enabled;
        SendButton.IsEnabled = enabled;
    }

    private void ShowMessage(string message)
    {
        MessageLabel.Text = message;
        MessageBanner.IsVisible = true;
    }

    private static string SenderLabel(ProviderMobileMissionMessageResponse message) => message.SenderType switch
    {
        "Provider" => $"Vous · {message.CreatedAt.LocalDateTime:HH:mm}",
        "Customer" => $"Client · {message.CreatedAt.LocalDateTime:HH:mm}",
        "Company" => $"Entreprise · {message.CreatedAt.LocalDateTime:HH:mm}",
        _ => $"{message.SenderType} · {message.CreatedAt.LocalDateTime:HH:mm}"
    };
    private static string BuildMessageSignature(IEnumerable<ProviderMobileMissionMessageResponse> messages)
        => string.Join('|', messages.Select(item => $"{item.MessageId:D}:{item.ReadAt?.ToUnixTimeMilliseconds() ?? 0}"));

    private static int StatusOrder(string status) => status switch { "Started" => 0, "OnTheWay" => 1, "Accepted" => 2, _ => 3 };
    private static bool IsConversationActive(string status)
        => status.Equals("Accepted", StringComparison.OrdinalIgnoreCase)
            || status.Equals("OnTheWay", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Started", StringComparison.OrdinalIgnoreCase);
    private static string FirstName(string value) => value.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? value;

    private static ColumnDefinitionCollection Columns(params GridLength[] widths)
    {
        var result = new ColumnDefinitionCollection();
        foreach (var width in widths) result.Add(new ColumnDefinition(width));
        return result;
    }
}
