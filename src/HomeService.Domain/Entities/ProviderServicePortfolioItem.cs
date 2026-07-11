using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class ProviderServicePortfolioItem : AuditableEntity
{
    private ProviderServicePortfolioItem()
    {
    }

    public ProviderServicePortfolioItem(
        Guid providerId,
        Guid serviceId,
        string originalFileName,
        string storagePath,
        string contentType,
        int displayOrder)
    {
        ProviderId = providerId;
        ServiceId = serviceId;
        OriginalFileName = originalFileName.Trim();
        StoragePath = storagePath.Trim();
        ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();
        DisplayOrder = displayOrder;
    }

    public Guid ProviderId { get; private set; }
    public ProviderProfile? Provider { get; private set; }
    public Guid ServiceId { get; private set; }
    public Service? Service { get; private set; }
    public string OriginalFileName { get; private set; } = string.Empty;
    public string StoragePath { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public int DisplayOrder { get; private set; }
    public PortfolioItemStatus Status { get; private set; } = PortfolioItemStatus.Pending;
    public string? RejectionReason { get; private set; }

    public void Approve()
    {
        Status = PortfolioItemStatus.Approved;
        RejectionReason = null;
        Touch();
    }

    public void Reject(string reason)
    {
        Status = PortfolioItemStatus.Rejected;
        RejectionReason = reason.Trim();
        Touch();
    }

    public void UpdateOrder(int displayOrder)
    {
        DisplayOrder = displayOrder;
        Touch();
    }
}
