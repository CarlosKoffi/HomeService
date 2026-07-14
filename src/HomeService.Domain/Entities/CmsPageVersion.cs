using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class CmsPageVersion : AuditableEntity
{
    private readonly List<CmsSection> _sections = [];

    private CmsPageVersion()
    {
    }

    public CmsPageVersion(Guid pageId, int versionNumber)
    {
        PageId = pageId;
        VersionNumber = versionNumber < 1 ? throw new ArgumentOutOfRangeException(nameof(versionNumber)) : versionNumber;
    }

    public Guid PageId { get; private set; }
    public CmsPage? Page { get; private set; }
    public int VersionNumber { get; private set; }
    public CmsPublicationStatus Status { get; private set; } = CmsPublicationStatus.Draft;
    public DateTimeOffset? PublishedAt { get; private set; }
    public IReadOnlyCollection<CmsSection> Sections => _sections;

    public void MarkPublished(DateTimeOffset publishedAt)
    {
        Status = CmsPublicationStatus.Published;
        PublishedAt = publishedAt;
        Touch();
    }
}
