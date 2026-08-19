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

    public async Task<ProviderInvitationActivationResult> ActivateInvitationAsync(
        ProviderInvitationActivationRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidatePassword(request.Password, request.ConfirmPassword);
        if (validationError is not null)
        {
            return ProviderInvitationActivationResult.Failed(validationError);
        }

        var normalizedCode = NormalizeCode(request.Code);
        var invitation = await db.ProviderInvitations
            .Include(invitation => invitation.Provider)
            .ThenInclude(provider => provider!.Company)
            .FirstOrDefaultAsync(invitation => invitation.Code == normalizedCode, cancellationToken);

        if (invitation?.Provider is null)
        {
            return ProviderInvitationActivationResult.Failed("Code de preinscription introuvable.");
        }

        if (!invitation.IsActive)
        {
            return ProviderInvitationActivationResult.Failed("Ce code est expire ou deja utilise.");
        }

        invitation.Provider.ActivateFromCompanyInvitation(Sha256PasswordHasher.Hash(request.Password));
        invitation.Accept();

        return ProviderInvitationActivationResult.Ok(
            new ProviderInvitationActivationResponse(
                invitation.Provider.Id,
                invitation.Code,
                invitation.Provider.FullName,
                invitation.Provider.PhoneNumber,
                invitation.Provider.Company?.Name ?? "Entreprise partenaire",
                true),
            invitation.Provider);
    }

    public async Task<ProviderPortalAuthResult> LoginAsync(
        ProviderPortalLoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber) || string.IsNullOrWhiteSpace(request.Password))
        {
            return ProviderPortalAuthResult.Failed("Telephone et mot de passe obligatoires.");
        }

        var phoneCandidates = BuildPhoneCandidates(request.PhoneNumber);
        if (phoneCandidates.Length == 0)
        {
            return ProviderPortalAuthResult.Failed("Identifiants prestataire invalides.");
        }

        var matchingProviders = await db.Providers
            .Include(provider => provider.Company)
            .Where(provider => phoneCandidates.Contains(
                provider.PhoneNumber
                    .Replace(" ", string.Empty)
                    .Replace("+", string.Empty)
                    .Replace("-", string.Empty)
                    .Replace("(", string.Empty)
                    .Replace(")", string.Empty)
                    .Replace(".", string.Empty)))
            .ToListAsync(cancellationToken);

        // Historical imports can contain the same phone number in different formats.
        // Verify the submitted password against every matching profile instead of
        // letting an older duplicate shadow the profile activated by the invitation.
        var provider = matchingProviders
            .Where(candidate =>
                !string.IsNullOrWhiteSpace(candidate.PasswordHash)
                && Sha256PasswordHasher.Verify(request.Password, candidate.PasswordHash))
            .OrderByDescending(candidate => candidate.Status == ProviderStatus.Approved)
            .FirstOrDefault();

        if (provider is null)
        {
            return ProviderPortalAuthResult.Failed("Identifiants prestataire invalides.");
        }

        if (Sha256PasswordHasher.NeedsRehash(provider.PasswordHash!))
        {
            provider.SetPortalPassword(Sha256PasswordHasher.Hash(request.Password));
        }

        if (provider.Status is ProviderStatus.Inactive or ProviderStatus.SuspendedByCompany or ProviderStatus.SuspendedByPlatform)
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

    private static string[] BuildPhoneCandidates(string phoneNumber)
    {
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits))
        {
            return [];
        }

        var candidates = new HashSet<string>(StringComparer.Ordinal)
        {
            digits
        };

        if (digits.StartsWith("00225", StringComparison.Ordinal) && digits.Length > 5)
        {
            digits = digits[2..];
            candidates.Add(digits);
        }

        if (digits.StartsWith("225", StringComparison.Ordinal) && digits.Length == 13)
        {
            var localNumber = digits[3..];
            candidates.Add(localNumber);
            candidates.Add($"00225{localNumber}");
        }
        else if (digits.Length == 10)
        {
            candidates.Add($"225{digits}");
            candidates.Add($"00225{digits}");
        }

        return [.. candidates];
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

public sealed record ProviderInvitationActivationResult(
    bool IsSuccess,
    ProviderInvitationActivationResponse? Response,
    ProviderProfile? Provider,
    string? ErrorMessage)
{
    public static ProviderInvitationActivationResult Ok(
        ProviderInvitationActivationResponse response,
        ProviderProfile provider)
        => new(true, response, provider, null);

    public static ProviderInvitationActivationResult Failed(string message)
        => new(false, null, null, message);
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
