using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class BusinessClientDocument : AuditableEntity
{
    private BusinessClientDocument()
    {
    }

    public BusinessClientDocument(
        Guid businessClientProfileId,
        BusinessClientDocumentType documentType,
        string originalFileName,
        string storagePath,
        string contentType,
        long size)
    {
        BusinessClientProfileId = businessClientProfileId;
        DocumentType = documentType;
        OriginalFileName = originalFileName.Trim();
        StoragePath = storagePath.Trim();
        ContentType = contentType.Trim();
        Size = size;
    }

    public Guid BusinessClientProfileId { get; private set; }
    public BusinessClientProfile? BusinessClientProfile { get; private set; }
    public BusinessClientDocumentType DocumentType { get; private set; }
    public string OriginalFileName { get; private set; } = string.Empty;
    public string StoragePath { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long Size { get; private set; }
    public DocumentReviewStatus ReviewStatus { get; private set; } = DocumentReviewStatus.Pending;
    public string? ReviewNote { get; private set; }

    public void Approve()
    {
        ReviewStatus = DocumentReviewStatus.Approved;
        ReviewNote = null;
        Touch();
    }

    public void RequestReplacement(string note)
    {
        ReviewStatus = DocumentReviewStatus.NeedsReplacement;
        ReviewNote = string.IsNullOrWhiteSpace(note) ? "Piece a remplacer." : note.Trim();
        Touch();
    }
}
