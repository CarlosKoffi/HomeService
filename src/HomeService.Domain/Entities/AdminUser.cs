using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class AdminUser : AuditableEntity
{
    private readonly List<AdminUserRole> _roles = [];

    private AdminUser()
    {
    }

    public AdminUser(string fullName, string email, bool isSuperAdmin = false)
    {
        FullName = fullName.Trim();
        Email = email.Trim().ToLowerInvariant();
        IsSuperAdmin = isSuperAdmin;
    }

    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public bool IsSuperAdmin { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset? LastLoginAt { get; private set; }
    public IReadOnlyCollection<AdminUserRole> Roles => _roles;

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }
}
