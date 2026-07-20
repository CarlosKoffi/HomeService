using System.Security.Cryptography;
using HomeService.Application.Abstractions;
using HomeService.Application.Security;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.CompanyPortal;

public sealed class CompanyEmployeeInvitationService(IAppDbContext db)
{
    public async Task<ProviderInvitation> CreatePendingInvitationAsync(
        Guid providerId,
        Guid companyId,
        string? providerPortalBaseUrl,
        CancellationToken cancellationToken)
    {
        var invitationCode = await GenerateUniqueCodeAsync(cancellationToken);
        var invitationToken = PortalTokenService.GenerateSecureToken();
        var invitation = new ProviderInvitation(
            providerId,
            companyId,
            invitationCode,
            PortalTokenService.HashToken(invitationToken),
            DateTimeOffset.UtcNow.AddDays(14));

        if (!string.IsNullOrWhiteSpace(providerPortalBaseUrl))
        {
            invitation.SetInvitationLink($"{providerPortalBaseUrl.TrimEnd('/')}/onboarding?code={Uri.EscapeDataString(invitation.Code)}");
        }

        db.ProviderInvitations.Add(invitation);

        return invitation;
    }

    public async Task<CompanyEmployeeInvitationResult> RegenerateAsync(
        Guid companyId,
        Guid providerId,
        string? providerPortalBaseUrl,
        CancellationToken cancellationToken)
    {
        var provider = await db.Providers
            .FirstOrDefaultAsync(provider => provider.Id == providerId && provider.CompanyId == companyId && provider.Status != ProviderStatus.Inactive, cancellationToken);
        if (provider is null)
        {
            return CompanyEmployeeInvitationResult.NotFound();
        }

        var pendingInvitations = await db.ProviderInvitations
            .Where(invitation => invitation.ProviderId == providerId && invitation.Status == ProviderInvitationStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var pendingInvitation in pendingInvitations)
        {
            pendingInvitation.Revoke();
        }

        var invitation = await CreatePendingInvitationAsync(provider.Id, companyId, providerPortalBaseUrl, cancellationToken);

        return CompanyEmployeeInvitationResult.Ok(provider.Id, invitation);
    }

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var code = $"WELE-{RandomNumberGenerator.GetInt32(100000, 999999)}";
            var exists = await db.ProviderInvitations.AnyAsync(invitation => invitation.Code == code, cancellationToken);
            if (!exists)
            {
                return code;
            }
        }

        return $"WELE-{Guid.NewGuid():N}"[..15].ToUpperInvariant();
    }
}

public enum CompanyEmployeeInvitationStatus
{
    Ok,
    NotFound
}

public sealed record CompanyEmployeeInvitationResult(
    CompanyEmployeeInvitationStatus Status,
    Guid? ProviderId,
    ProviderInvitation? Invitation,
    string Message)
{
    public static CompanyEmployeeInvitationResult Ok(Guid providerId, ProviderInvitation invitation)
        => new(CompanyEmployeeInvitationStatus.Ok, providerId, invitation, "Code d'acces genere.");

    public static CompanyEmployeeInvitationResult NotFound()
        => new(CompanyEmployeeInvitationStatus.NotFound, null, null, "Prestataire introuvable.");
}
