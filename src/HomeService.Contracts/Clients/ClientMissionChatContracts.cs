namespace HomeService.Contracts.Clients;

public sealed record ClientMissionChatResponse(
    Guid MissionId,
    string MissionNumber,
    string MissionLabel,
    Guid ConversationId,
    IReadOnlyList<ClientMissionMessageResponse> Messages,
    string? ProviderName = null,
    string? ProviderPhotoUrl = null);

public sealed record ClientMissionMessageResponse(
    Guid MessageId,
    string SenderType,
    string Body,
    string? AttachmentPath,
    string? AttachmentContentType,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

public sealed record SendClientMissionMessageRequest(
    string PhoneNumber,
    string Body,
    string? AttachmentPath,
    string? AttachmentContentType);

public sealed record SendClientMissionMessageResponse(
    Guid MissionId,
    Guid ConversationId,
    Guid MessageId,
    DateTimeOffset CreatedAt,
    string Message);
