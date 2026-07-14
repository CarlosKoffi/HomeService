using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class CmsMenu : AuditableEntity
{
    private readonly List<CmsMenuItem> _items = [];

    private CmsMenu()
    {
    }

    public CmsMenu(Guid siteId, string code, string name, string placement)
    {
        SiteId = siteId;
        Code = NormalizeCode(code);
        Name = NormalizeName(name);
        Placement = NormalizeCode(placement);
    }

    public Guid SiteId { get; private set; }
    public CmsSite? Site { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Placement { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public IReadOnlyCollection<CmsMenuItem> Items => _items;

    private static string NormalizeCode(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A CMS menu code is required.", nameof(value))
            : value.Trim().ToLowerInvariant();
    }

    private static string NormalizeName(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A CMS menu name is required.", nameof(value))
            : value.Trim();
    }
}
