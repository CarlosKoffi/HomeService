namespace HomeService.Contracts.ProviderPortal;

public sealed record ProviderMissionChatResponse(
    Guid AssignmentId,
    Guid MissionId,
    Guid ConversationId,
    IReadOnlyList<ProviderMobileMissionMessageResponse> Messages);

public sealed record SendProviderMissionMessageRequest(
    string Body,
    string? AttachmentPath = null,
    string? AttachmentContentType = null);

public sealed record SendProviderMissionMessageResponse(
    Guid AssignmentId,
    Guid MissionId,
    Guid ConversationId,
    Guid MessageId,
    DateTimeOffset CreatedAt,
    string Message);
