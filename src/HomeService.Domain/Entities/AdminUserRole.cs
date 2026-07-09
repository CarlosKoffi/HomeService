using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class AdminUserRole : AuditableEntity
{
    private AdminUserRole()
    {
    }

    public AdminUserRole(Guid adminUserId, Guid roleId)
    {
        AdminUserId = adminUserId;
        RoleId = roleId;
    }

    public Guid AdminUserId { get; private set; }
    public AdminUser? AdminUser { get; private set; }
    public Guid RoleId { get; private set; }
    public AdminRole? Role { get; private set; }
}
