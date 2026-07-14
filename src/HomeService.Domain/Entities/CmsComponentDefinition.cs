using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class CmsComponentDefinition : AuditableEntity
{
    private CmsComponentDefinition()
    {
    }

    public CmsComponentDefinition(string key, string name, int schemaVersion, string? description = null)
    {
        Key = NormalizeKey(key);
        Name = NormalizeName(name);
        SchemaVersion = schemaVersion < 1 ? 1 : schemaVersion;
        Description = description?.Trim();
    }

    public string Key { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int SchemaVersion { get; private set; } = 1;
    public bool IsActive { get; private set; } = true;

    private static string NormalizeKey(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A CMS component key is required.", nameof(value))
            : value.Trim();
    }

    private static string NormalizeName(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A CMS component name is required.", nameof(value))
            : value.Trim();
    }
}
