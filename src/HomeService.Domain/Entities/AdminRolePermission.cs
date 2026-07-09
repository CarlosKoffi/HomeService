using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class AdminRolePermission : AuditableEntity
{
    private AdminRolePermission()
    {
    }

    public AdminRolePermission(Guid roleId, Guid moduleId, AdminPermissionAction action)
    {
        RoleId = roleId;
        ModuleId = moduleId;
        Action = action;
    }

    public Guid RoleId { get; private set; }
    public AdminRole? Role { get; private set; }
    public Guid ModuleId { get; private set; }
    public AdminModule? Module { get; private set; }
    public AdminPermissionAction Action { get; private set; }
}
