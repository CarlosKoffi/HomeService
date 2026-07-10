using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class CompanyApplicationDocument : AuditableEntity
{
    private CompanyApplicationDocument()
    {
    }

    public CompanyApplicationDocument(
        Guid companyApplicationId,
        CompanyDocumentType documentType,
        string originalFileName,
        string storagePath,
        string contentType)
    {
        CompanyApplicationId = companyApplicationId;
        DocumentType = documentType;
        OriginalFileName = originalFileName.Trim();
        StoragePath = storagePath.Trim();
        ContentType = contentType.Trim();
    }

    public Guid CompanyApplicationId { get; private set; }
    public CompanyApplication? CompanyApplication { get; private set; }
    public CompanyDocumentType DocumentType { get; private set; }
    public string OriginalFileName { get; private set; } = string.Empty;
    public string StoragePath { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public DocumentReviewStatus ReviewStatus { get; private set; } = DocumentReviewStatus.Pending;
    public string? ReviewNote { get; private set; }

    public void Approve()
    {
        ReviewStatus = DocumentReviewStatus.Approved;
        Touch();
    }

    public void RequestReplacement(string note)
    {
        ReviewStatus = DocumentReviewStatus.NeedsReplacement;
        ReviewNote = note.Trim();
        Touch();
    }

    public void Reject(string note)
    {
        ReviewStatus = DocumentReviewStatus.Rejected;
        ReviewNote = note.Trim();
        Touch();
    }

    public void Reopen(string note)
    {
        if (ReviewStatus != DocumentReviewStatus.Rejected)
        {
            throw new InvalidOperationException("Seule une piece refusee peut etre reouverte.");
        }

        ReviewStatus = DocumentReviewStatus.Pending;
        ReviewNote = note.Trim();
        Touch();
    }
}
