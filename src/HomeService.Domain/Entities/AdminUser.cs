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
    public string? PasswordHash { get; private set; }
    public string? InvitationTokenHash { get; private set; }
    public DateTimeOffset? InvitationExpiresAt { get; private set; }
    public DateTimeOffset? InvitationAcceptedAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public IReadOnlyCollection<AdminUserRole> Roles => _roles;

    public void SetInvitation(string tokenHash, DateTimeOffset expiresAt)
    {
        InvitationTokenHash = tokenHash.Trim();
        InvitationExpiresAt = expiresAt;
        InvitationAcceptedAt = null;
        Touch();
    }

    public void UpdateProfile(string fullName, string email)
    {
        FullName = fullName.Trim();
        Email = email.Trim().ToLowerInvariant();
        Touch();
    }

    public void AcceptInvitation(string passwordHash, DateTimeOffset acceptedAt)
    {
        PasswordHash = passwordHash.Trim();
        InvitationTokenHash = null;
        InvitationExpiresAt = null;
        InvitationAcceptedAt = acceptedAt;
        IsActive = true;
        Touch();
    }

    public void SetPasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash.Trim();
        Touch();
    }

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }

    public void Reactivate()
    {
        IsActive = true;
        Touch();
    }

    public void PromoteToSuperAdmin()
    {
        IsSuperAdmin = true;
        IsActive = true;
        Touch();
    }

    public void RecordLogin(DateTimeOffset loginAt)
    {
        LastLoginAt = loginAt;
        Touch();
    }
}
