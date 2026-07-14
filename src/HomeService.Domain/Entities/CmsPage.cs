using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class CmsPage : AuditableEntity
{
    private readonly List<CmsPageTranslation> _translations = [];
    private readonly List<CmsPageVersion> _versions = [];

    private CmsPage()
    {
    }

    public CmsPage(Guid siteId, string code, string internalName, string templateKey)
    {
        SiteId = siteId;
        Code = NormalizeCode(code);
        InternalName = NormalizeName(internalName);
        TemplateKey = NormalizeCode(templateKey);
    }

    public Guid SiteId { get; private set; }
    public CmsSite? Site { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string InternalName { get; private set; } = string.Empty;
    public string TemplateKey { get; private set; } = string.Empty;
    public CmsPublicationStatus Status { get; private set; } = CmsPublicationStatus.Draft;
    public bool RequiresAuthentication { get; private set; }
    public IReadOnlyCollection<CmsPageTranslation> Translations => _translations;
    public IReadOnlyCollection<CmsPageVersion> Versions => _versions;

    public void RequireAuthentication()
    {
        RequiresAuthentication = true;
        Touch();
    }

    private static string NormalizeCode(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A CMS page code is required.", nameof(value))
            : value.Trim().ToLowerInvariant();
    }

    private static string NormalizeName(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A CMS page name is required.", nameof(value))
            : value.Trim();
    }
}
