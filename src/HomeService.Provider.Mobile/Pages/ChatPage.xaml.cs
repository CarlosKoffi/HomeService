using HomeService.Contracts.ProviderPortal;
using HomeService.Provider.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Storage;

namespace HomeService.Provider.Mobile.Pages;

public partial class ChatPage : ContentPage
{
    private const string AccessTokenPreferenceKey = "ProviderAccessToken";
    private readonly ProviderMobileApiClient? apiClient;
    private string? accessToken;
    private Guid? assignmentId;

    public ChatPage()
    {
        InitializeComponent();
        apiClient = IPlatformApplication.Current?.Services.GetService<ProviderMobileApiClient>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadChatAsync();
    }

    private async Task LoadChatAsync()
    {
        if (apiClient is null)
        {
            ShowMessage("Configuration mobile incomplete. Client API introuvable.");
            SetComposerEnabled(false);
            return;
        }

        accessToken = Preferences.Default.Get(AccessTokenPreferenceKey, string.Empty);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            ShowMessage("Connectez-vous pour consulter vos messages.");
            SetComposerEnabled(false);
            return;
        }

        var homeResult = await apiClient.GetHomeResultAsync(accessToken);
        if (!homeResult.IsSuccess || homeResult.Response is null)
        {
            ShowMessage(homeResult.ErrorMessage ?? "Impossible de charger la mission courante.");
            SetComposerEnabled(false);
            return;
        }

        assignmentId = homeResult.Response.LiveOffer?.AssignmentId ?? homeResult.Response.UpcomingMission?.AssignmentId;
        if (assignmentId is null)
        {
            MissionTitleLabel.Text = "Aucune mission active";
            MissionSubtitleLabel.Text = "Les messages seront disponibles quand une mission sera affectee.";
            MissionNumberLabel.Text = string.Empty;
            MessagesStack.Children.Clear();
            SetComposerEnabled(false);
            return;
        }

        var detailResult = await apiClient.GetMissionDetailAsync(accessToken, assignmentId.Value);
        var chatResult = await apiClient.GetMissionMessagesAsync(accessToken, assignmentId.Value);
        if (!chatResult.IsSuccess || chatResult.Response is null)
        {
            ShowMessage(chatResult.ErrorMessage ?? "Impossible de charger les messages.");
            SetComposerEnabled(false);
            return;
        }


        MissionTitleLabel.Text = chatResult.Response.MissionLabel;
        MissionNumberLabel.Text = $"Mission {chatResult.Response.MissionNumber}";
        MissionSubtitleLabel.Text = detailResult.Response is null
            ? "Conversation avec le client de cette mission."
            : $"{detailResult.Response.CustomerDisplayName} - {detailResult.Response.LocationLabel}";

        HideMessage();
        RenderMessages(chatResult.Response.Messages);
        SetComposerEnabled(true);
    }

    private void RenderMessages(IReadOnlyList<ProviderMobileMissionMessageResponse> messages)
    {
        MessagesStack.Children.Clear();
        if (messages.Count == 0)
        {
            MessagesStack.Children.Add(CreateMessageCard("Aucun message pour le moment.", "Systeme", false));
            return;
        }

        foreach (var message in messages.OrderBy(item => item.CreatedAt))
        {
            var isProvider = message.SenderType.Equals("Provider", StringComparison.OrdinalIgnoreCase);
            MessagesStack.Children.Add(CreateMessageCard(message.Body, BuildSenderLabel(message), isProvider));
        }
    }

    private static View CreateMessageCard(string body, string senderLabel, bool isProvider)
    {
        return new Border
        {
            BackgroundColor = isProvider ? Color.FromArgb("#EEF4FF") : Colors.White,
            Stroke = isProvider ? Color.FromArgb("#CFE0FF") : Color.FromArgb("#E6E9EF"),
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Padding = 14,
            Content = new VerticalStackLayout
            {
                Spacing = 5,
                Children =
                {
                    new Label
                    {
                        Text = senderLabel,
                        FontAttributes = FontAttributes.Bold,
                        FontSize = 13,
                        TextColor = Colors.Black
                    },
                    new Label
                    {
                        Text = body,
                        FontSize = 14,
                        TextColor = Colors.Black,
                        LineHeight = 1.25
                    }
                }
            }
        };
    }

    private async void OnSendClicked(object? sender, EventArgs e)
    {
        var body = MessageEntry.Text?.Trim();
        if (apiClient is null || assignmentId is null || string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        SetComposerEnabled(false);
        var result = await apiClient.SendMissionMessageAsync(
            accessToken,
            assignmentId.Value,
            new SendProviderMissionMessageRequest(body));

        if (!result.IsSuccess)
        {
            ShowMessage(result.ErrorMessage ?? "Message non envoye.");
            SetComposerEnabled(true);
            return;
        }

        MessageEntry.Text = string.Empty;
        await LoadChatAsync();
    }

    private void SetComposerEnabled(bool isEnabled)
    {
        MessageEntry.IsEnabled = isEnabled;
        SendButton.IsEnabled = isEnabled;
    }

    private void ShowMessage(string message)
    {
        MessageLabel.Text = message;
        MessageBanner.IsVisible = true;
    }

    private void HideMessage()
    {
        MessageBanner.IsVisible = false;
    }

    private static string BuildSenderLabel(ProviderMobileMissionMessageResponse message)
    {
        return message.SenderType switch
        {
            "Provider" => $"Vous - {message.CreatedAt.LocalDateTime:g}",
            "Customer" => $"Client - {message.CreatedAt.LocalDateTime:g}",
            "Company" => $"Entreprise - {message.CreatedAt.LocalDateTime:g}",
            _ => $"{message.SenderType} - {message.CreatedAt.LocalDateTime:g}"
        };
    }
}
