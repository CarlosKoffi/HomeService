using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class AdminModule : AuditableEntity
{
    private AdminModule()
    {
    }

    public AdminModule(AdminModuleKey key, string name, string description, int displayOrder)
    {
        Key = key;
        Name = name.Trim();
        Description = description.Trim();
        DisplayOrder = displayOrder;
    }

    public AdminModuleKey Key { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; } = true;
}
