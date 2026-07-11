using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class MissionMessage : AuditableEntity
{
    private MissionMessage()
    {
    }

    public MissionMessage(
        Guid conversationId,
        MissionMessageSenderType senderType,
        Guid? senderId,
        string body,
        string? attachmentPath,
        string? attachmentContentType)
    {
        ConversationId = conversationId;
        SenderType = senderType;
        SenderId = senderId;
        Body = body.Trim();
        AttachmentPath = attachmentPath;
        AttachmentContentType = attachmentContentType;
    }

    public Guid ConversationId { get; private set; }
    public MissionConversation? Conversation { get; private set; }
    public MissionMessageSenderType SenderType { get; private set; }
    public Guid? SenderId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public string? AttachmentPath { get; private set; }
    public string? AttachmentContentType { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    public void MarkRead()
    {
        ReadAt = DateTimeOffset.UtcNow;
        Touch();
    }
}
