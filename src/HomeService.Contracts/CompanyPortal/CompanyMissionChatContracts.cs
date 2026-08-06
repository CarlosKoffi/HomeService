namespace HomeService.Contracts.CompanyPortal;

public sealed record CompanyMissionChatResponse(
    Guid MissionId,
    string MissionNumber,
    string MissionLabel,
    string CustomerName,
    string? ProviderName,
    Guid ConversationId,
    IReadOnlyList<CompanyMissionMessageResponse> Messages);

public sealed record CompanyMissionMessageResponse(
    Guid MessageId,
    string SenderType,
    string Body,
    string? AttachmentPath,
    string? AttachmentContentType,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

public sealed record SendCompanyMissionMessageRequest(
    string Body,
    string? AttachmentPath = null,
    string? AttachmentContentType = null);

public sealed record SendCompanyMissionMessageResponse(
    Guid MissionId,
    Guid ConversationId,
    Guid MessageId,
    DateTimeOffset CreatedAt,
    string Message);
