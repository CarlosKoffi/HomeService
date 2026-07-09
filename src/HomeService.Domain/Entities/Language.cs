using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class Language : AuditableEntity
{
    private Language()
    {
    }

    public Language(string code, string name, bool isDefault = false)
    {
        Code = code.Trim().ToLowerInvariant();
        Name = name.Trim();
        IsDefault = isDefault;
    }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; } = true;
}
