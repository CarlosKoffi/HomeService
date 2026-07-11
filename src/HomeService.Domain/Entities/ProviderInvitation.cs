using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class ProviderInvitation : AuditableEntity
{
    private ProviderInvitation()
    {
    }

    public ProviderInvitation(Guid providerId, Guid companyId, string code, string tokenHash, DateTimeOffset expiresAt)
    {
        ProviderId = providerId;
        CompanyId = companyId;
        Code = code.Trim().ToUpperInvariant();
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public Guid ProviderId { get; private set; }
    public ProviderProfile? Provider { get; private set; }
    public Guid CompanyId { get; private set; }
    public Company? Company { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public ProviderInvitationStatus Status { get; private set; } = ProviderInvitationStatus.Pending;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? InvitationLink { get; private set; }

    public bool IsActive => Status == ProviderInvitationStatus.Pending && RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;

    public void SetInvitationLink(string invitationLink)
    {
        InvitationLink = invitationLink.Trim();
        Touch();
    }

    public void Accept()
    {
        Status = ProviderInvitationStatus.Accepted;
        AcceptedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void Revoke()
    {
        Status = ProviderInvitationStatus.Revoked;
        RevokedAt = DateTimeOffset.UtcNow;
        Touch();
    }
}
