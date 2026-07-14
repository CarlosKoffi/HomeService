using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class CmsSite : AuditableEntity
{
    private readonly List<CmsPage> _pages = [];
    private readonly List<CmsMenu> _menus = [];

    private CmsSite()
    {
    }

    public CmsSite(string code, string name, CmsSiteSurface surface, Guid? defaultCountryId, Guid defaultLanguageId)
    {
        Code = NormalizeCode(code);
        Name = NormalizeName(name);
        Surface = surface;
        DefaultCountryId = defaultCountryId;
        DefaultLanguageId = defaultLanguageId;
    }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public CmsSiteSurface Surface { get; private set; }
    public CmsSiteStatus Status { get; private set; } = CmsSiteStatus.Draft;
    public Guid? DefaultCountryId { get; private set; }
    public Country? DefaultCountry { get; private set; }
    public Guid DefaultLanguageId { get; private set; }
    public Language? DefaultLanguage { get; private set; }
    public string? HomePageCode { get; private set; }
    public IReadOnlyCollection<CmsPage> Pages => _pages;
    public IReadOnlyCollection<CmsMenu> Menus => _menus;

    public void Activate()
    {
        Status = CmsSiteStatus.Active;
        Touch();
    }

    public void SetHomePage(string pageCode)
    {
        HomePageCode = NormalizeCode(pageCode);
        Touch();
    }

    private static string NormalizeCode(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A CMS site code is required.", nameof(value))
            : value.Trim().ToLowerInvariant();
    }

    private static string NormalizeName(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A CMS site name is required.", nameof(value))
            : value.Trim();
    }
}
