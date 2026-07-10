using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class CompanyPortalUser : AuditableEntity
{
    private CompanyPortalUser()
    {
    }

    public CompanyPortalUser(Guid companyId, string fullName, string email, string passwordHash, bool isOwner)
    {
        CompanyId = companyId;
        FullName = fullName.Trim();
        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash.Trim();
        IsOwner = isOwner;
    }

    public Guid CompanyId { get; private set; }
    public Company? Company { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public bool IsOwner { get; private set; }
    public bool IsActive { get; private set; } = true;
}
