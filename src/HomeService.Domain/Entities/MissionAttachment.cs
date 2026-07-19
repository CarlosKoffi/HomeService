using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class MissionAttachment : AuditableEntity
{
    private MissionAttachment()
    {
    }

    public MissionAttachment(
        Guid missionId,
        MissionAttachmentType attachmentType,
        string originalFileName,
        string storagePath,
        string contentType,
        long fileSizeBytes,
        string? caption = null)
    {
        MissionId = missionId;
        AttachmentType = attachmentType;
        OriginalFileName = originalFileName.Trim();
        StoragePath = storagePath.Trim();
        ContentType = contentType.Trim();
        FileSizeBytes = Math.Max(0, fileSizeBytes);
        Caption = Clean(caption);
    }

    public Guid MissionId { get; private set; }
    public Mission? Mission { get; private set; }
    public MissionAttachmentType AttachmentType { get; private set; }
    public string OriginalFileName { get; private set; } = string.Empty;
    public string StoragePath { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public string? Caption { get; private set; }
    public bool IsDeleted { get; private set; }

    public void RenameCaption(string? caption)
    {
        Caption = Clean(caption);
        Touch();
    }

    public void Delete()
    {
        IsDeleted = true;
        Touch();
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
