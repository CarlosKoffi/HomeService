using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class CmsPageTranslation : AuditableEntity
{
    private CmsPageTranslation()
    {
    }

    public CmsPageTranslation(Guid siteId, Guid pageId, Guid languageId, string slug, string title, string? metaDescription = null)
    {
        SiteId = siteId;
        PageId = pageId;
        LanguageId = languageId;
        Slug = NormalizeSlug(slug);
        Title = NormalizeTitle(title);
        MetaDescription = metaDescription?.Trim();
    }

    public Guid SiteId { get; private set; }
    public CmsSite? Site { get; private set; }
    public Guid PageId { get; private set; }
    public CmsPage? Page { get; private set; }
    public Guid LanguageId { get; private set; }
    public Language? Language { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string? SeoTitle { get; private set; }
    public string? MetaDescription { get; private set; }
    public CmsPublicationStatus TranslationStatus { get; private set; } = CmsPublicationStatus.Draft;

    private static string NormalizeSlug(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A CMS page slug is required.", nameof(value))
            : value.Trim().Trim('/').ToLowerInvariant();
    }

    private static string NormalizeTitle(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A CMS page title is required.", nameof(value))
            : value.Trim();
    }
}
