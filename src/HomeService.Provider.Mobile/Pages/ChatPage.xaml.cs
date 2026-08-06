using HomeService.Contracts.ProviderPortal;
using HomeService.Provider.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace HomeService.Provider.Mobile.Pages;

[QueryProperty(nameof(AssignmentId), "assignmentId")]
public partial class ChatPage : ContentPage
{
    private readonly ProviderMobileApiClient? apiClient;
    private readonly ProviderSessionService? sessionService;
    private readonly Dictionary<Guid, Border> conversationCards = [];
    private string? accessToken;
    private Guid? requestedAssignmentId;
    private Guid? assignmentId;
    private IReadOnlyList<ProviderMobileMissionSummaryResponse> missions = [];

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
        SetComposerEnabled(false);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadConversationsAsync();
    }

    private async Task LoadConversationsAsync()
    {
        MessageBanner.IsVisible = false;
        if (apiClient is null || sessionService is null)
        {
            ShowMessage("Configuration mobile incomplète. Client API introuvable.");
            RenderConversationChoices([]);
            return;
        }

        accessToken = await sessionService.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            ShowMessage("Connectez-vous pour consulter vos messages.");
            RenderConversationChoices([]);
            return;
        }

        var result = await apiClient.GetMissionsAsync(accessToken);
        if (!result.IsSuccess || result.Response is null)
        {
            ShowMessage(result.ErrorMessage ?? "Impossible de charger les conversations.");
            RenderConversationChoices([]);
            return;
        }

        missions = result.Response.Items
            .Where(item => item.Status is "Offered" or "Accepted" or "Started" or "Completed")
            .OrderBy(item => StatusOrder(item.Status))
            .ThenByDescending(item => item.ScheduledFor ?? DateTimeOffset.MinValue)
            .Take(20)
            .ToList();
        RenderConversationChoices(missions);

        var selectedId = requestedAssignmentId is not null && missions.Any(item => item.AssignmentId == requestedAssignmentId)
            ? requestedAssignmentId
            : assignmentId is not null && missions.Any(item => item.AssignmentId == assignmentId)
                ? assignmentId
                : missions.FirstOrDefault()?.AssignmentId;
        requestedAssignmentId = null;
        if (selectedId is not null) await SelectConversationAsync(selectedId.Value);
        else RenderNoConversation();
    }

    private void RenderConversationChoices(IReadOnlyList<ProviderMobileMissionSummaryResponse> items)
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
            var grid = new Grid { ColumnDefinitions = Columns(GridLength.Auto, GridLength.Auto), ColumnSpacing = 8 };
            grid.Add(new Image { Source = ProviderIconResolver.ForService(mission.ServiceIconName, mission.ServiceName), WidthRequest = 22, HeightRequest = 22 }, 0);
            grid.Add(new VerticalStackLayout
            {
                Spacing = 1,
                Children =
                {
                    new Label { Text = title, FontFamily = "PlusJakartaSans", FontSize = 12, FontAttributes = FontAttributes.Bold, MaxLines = 1 },
                    new Label { Text = mission.MissionNumber, FontFamily = "PlusJakartaSans", FontSize = 9, TextColor = Color.FromArgb("#667085") }
                }
            }, 1);
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
        RenderMessages(chatResult.Response.Messages);
        SetComposerEnabled(mission.Status is "Offered" or "Accepted" or "Started");
        await Task.Delay(50);
        await MessagesScroll.ScrollToAsync(MessagesStack, ScrollToPosition.End, false);
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
            var bubble = new Border
            {
                BackgroundColor = Color.FromArgb(mine ? "#155EEF" : "#F8FAFC"),
                Stroke = Color.FromArgb(mine ? "#155EEF" : "#E6E9EF"),
                StrokeShape = new RoundRectangle { CornerRadius = 18 },
                Padding = new Thickness(13, 10),
                HorizontalOptions = mine ? LayoutOptions.End : LayoutOptions.Start,
                MaximumWidthRequest = 310,
                Content = new VerticalStackLayout
                {
                    Spacing = 4,
                    Children =
                    {
                        new Label { Text = SenderLabel(message), FontFamily = "PlusJakartaSans", FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(mine ? "#DDE8FF" : "#667085") },
                        new Label { Text = message.Body, FontFamily = "PlusJakartaSans", FontSize = 14, TextColor = Color.FromArgb(mine ? "#FFFFFF" : "#0F172A"), LineHeight = 1.2 }
                    }
                }
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

    private static int StatusOrder(string status) => status switch { "Started" => 0, "Accepted" => 1, "Offered" => 2, _ => 3 };
    private static string FirstName(string value) => value.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? value;

    private static ColumnDefinitionCollection Columns(params GridLength[] widths)
    {
        var result = new ColumnDefinitionCollection();
        foreach (var width in widths) result.Add(new ColumnDefinition(width));
        return result;
    }
}
