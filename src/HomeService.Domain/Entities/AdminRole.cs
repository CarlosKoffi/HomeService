using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class AdminRole : AuditableEntity
{
    private readonly List<AdminRolePermission> _permissions = [];

    private AdminRole()
    {
    }

    public AdminRole(string name, string description)
    {
        Name = name.Trim();
        Description = description.Trim();
    }

    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool IsSystemRole { get; private set; }
    public bool IsActive { get; private set; } = true;
    public IReadOnlyCollection<AdminRolePermission> Permissions => _permissions;
}
