using HomeService.Application.Abstractions;
using HomeService.Application.Security;
using HomeService.Contracts.ProviderPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.ProviderPortal;

public sealed class ProviderPortalAuthService(IAppDbContext db)
{
    public async Task<ProviderInvitationPreviewResponse?> GetInvitationAsync(string code, CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCode(code);
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return null;
        }

        var invitation = await db.ProviderInvitations
            .AsNoTracking()
            .Include(invitation => invitation.Provider)
            .Include(invitation => invitation.Company)
            .FirstOrDefaultAsync(invitation => invitation.Code == normalizedCode, cancellationToken);

        if (invitation?.Provider is null || invitation.Company is null)
        {
            return null;
        }

        return new ProviderInvitationPreviewResponse(
            invitation.Id,
            invitation.ProviderId,
            invitation.Code,
            invitation.Provider.FullName,
            invitation.Provider.PhoneNumber,
            invitation.Company.Name,
            invitation.Status.ToString(),
            invitation.ExpiresAt);
    }

    public async Task<ProviderPortalAuthResult> ActivateInvitationAsync(
        ProviderInvitationActivationRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidatePassword(request.Password, request.ConfirmPassword);
        if (validationError is not null)
        {
            return ProviderPortalAuthResult.Failed(validationError);
        }

        var normalizedCode = NormalizeCode(request.Code);
        var invitation = await db.ProviderInvitations
            .Include(invitation => invitation.Provider)
            .ThenInclude(provider => provider!.Company)
            .FirstOrDefaultAsync(invitation => invitation.Code == normalizedCode, cancellationToken);

        if (invitation?.Provider is null)
        {
            return ProviderPortalAuthResult.Failed("Code de preinscription introuvable.");
        }

        if (!invitation.IsActive)
        {
            return ProviderPortalAuthResult.Failed("Ce code est expire ou deja utilise.");
        }

        invitation.Provider.ActivateFromCompanyInvitation(Sha256PasswordHasher.Hash(request.Password));
        invitation.Accept();

        return await CreateSessionAsync(invitation.Provider, request.RememberMe, cancellationToken);
    }

    public async Task<ProviderPortalAuthResult> LoginAsync(
        ProviderPortalLoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber) || string.IsNullOrWhiteSpace(request.Password))
        {
            return ProviderPortalAuthResult.Failed("Telephone et mot de passe obligatoires.");
        }

        var phone = NormalizePhone(request.PhoneNumber);
        var provider = await db.Providers
            .Include(provider => provider.Company)
            .FirstOrDefaultAsync(provider => provider.PhoneNumber == phone, cancellationToken);

        if (provider is null || string.IsNullOrWhiteSpace(provider.PasswordHash) || !Sha256PasswordHasher.Verify(request.Password, provider.PasswordHash))
        {
            return ProviderPortalAuthResult.Failed("Identifiants prestataire invalides.");
        }

        if (provider.Status is ProviderStatus.Inactive or ProviderStatus.SuspendedByCompany)
        {
            return ProviderPortalAuthResult.Failed("Votre acces prestataire est suspendu.");
        }

        return await CreateSessionAsync(provider, request.RememberMe, cancellationToken);
    }

    private async Task<ProviderPortalAuthResult> CreateSessionAsync(
        ProviderProfile provider,
        bool rememberMe,
        CancellationToken cancellationToken)
    {
        var token = PortalTokenService.GenerateSecureToken();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(rememberMe ? 30 : 1);
        var session = new ProviderPortalSession(provider.Id, PortalTokenService.HashToken(token), expiresAt);
        db.ProviderPortalSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        return ProviderPortalAuthResult.Ok(
            new ProviderPortalLoginResponse(
                token,
                expiresAt,
                provider.Id,
                provider.FullName,
                provider.PhoneNumber,
                provider.Company?.Name,
                provider.Status.ToString(),
                CanReceiveMissions(provider)),
            session,
            provider);
    }

    private static bool CanReceiveMissions(ProviderProfile provider)
    {
        return provider.Status == ProviderStatus.Approved && provider.CompanyId is not null;
    }

    private static string NormalizeCode(string? code)
    {
        return (code ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", string.Empty);
    }

    private static string NormalizePhone(string phoneNumber)
    {
        return phoneNumber.Trim();
    }

    private static string? ValidatePassword(string password, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            return "Le mot de passe doit contenir au moins 8 caracteres.";
        }

        if (password != confirmPassword)
        {
            return "Les deux mots de passe ne correspondent pas.";
        }

        return null;
    }
}

public sealed record ProviderPortalAuthResult(
    bool IsSuccess,
    ProviderPortalLoginResponse? Response,
    ProviderPortalSession? Session,
    ProviderProfile? Provider,
    string? ErrorMessage)
{
    public static ProviderPortalAuthResult Ok(
        ProviderPortalLoginResponse response,
        ProviderPortalSession session,
        ProviderProfile provider)
        => new(true, response, session, provider, null);

    public static ProviderPortalAuthResult Failed(string message)
        => new(false, null, null, null, message);
}
