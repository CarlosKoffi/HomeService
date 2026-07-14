using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class CmsSection : AuditableEntity
{
    private readonly List<CmsContentValue> _contentValues = [];

    private CmsSection()
    {
    }

    public CmsSection(Guid pageVersionId, Guid componentDefinitionId, string internalName, string zone, int position)
    {
        PageVersionId = pageVersionId;
        ComponentDefinitionId = componentDefinitionId;
        InternalName = NormalizeName(internalName);
        Zone = NormalizeZone(zone);
        Position = position < 0 ? throw new ArgumentOutOfRangeException(nameof(position)) : position;
    }

    public Guid PageVersionId { get; private set; }
    public CmsPageVersion? PageVersion { get; private set; }
    public Guid ComponentDefinitionId { get; private set; }
    public CmsComponentDefinition? ComponentDefinition { get; private set; }
    public string InternalName { get; private set; } = string.Empty;
    public string Zone { get; private set; } = "main";
    public int Position { get; private set; }
    public string? Anchor { get; private set; }
    public string? Variant { get; private set; }
    public bool IsActive { get; private set; } = true;
    public IReadOnlyCollection<CmsContentValue> ContentValues => _contentValues;

    private static string NormalizeName(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A CMS section name is required.", nameof(value))
            : value.Trim();
    }

    private static string NormalizeZone(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "main" : value.Trim().ToLowerInvariant();
    }
}
