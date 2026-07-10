using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class ProviderDocument : AuditableEntity
{
    private ProviderDocument()
    {
    }

    public ProviderDocument(
        Guid providerId,
        ProviderDocumentType documentType,
        string originalFileName,
        string storagePath,
        string contentType)
    {
        ProviderId = providerId;
        DocumentType = documentType;
        OriginalFileName = originalFileName.Trim();
        StoragePath = storagePath.Trim();
        ContentType = contentType.Trim();
    }

    public Guid ProviderId { get; private set; }
    public ProviderProfile? Provider { get; private set; }
    public ProviderDocumentType DocumentType { get; private set; }
    public string OriginalFileName { get; private set; } = string.Empty;
    public string StoragePath { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
}
