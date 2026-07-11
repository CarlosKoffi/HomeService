using HomeService.Application.Abstractions;
using HomeService.Application.Security;
using HomeService.Contracts.CompanyPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.CompanyPortal;

public sealed class CompanyPortalAuthService(IAppDbContext db)
{
    public async Task<CompanyPortalLoginResult> LoginAsync(
        CompanyPortalLoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return CompanyPortalLoginResult.MissingCredentials();
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.CompanyPortalUsers
            .Include(user => user.Company)
            .FirstOrDefaultAsync(user => user.Email == email && user.IsActive, cancellationToken);

        if (user is null || user.Company is null || !Sha256PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            return CompanyPortalLoginResult.InvalidCredentials();
        }

        if (user.Company.Status == CompanyStatus.Suspended)
        {
            return CompanyPortalLoginResult.Suspended();
        }

        var token = PortalTokenService.GenerateSecureToken();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(request.RememberMe ? 30 : 1);
        var session = new CompanyPortalSession(user.Id, PortalTokenService.HashToken(token), expiresAt);
        db.CompanyPortalSessions.Add(session);

        return CompanyPortalLoginResult.Ok(
            new CompanyPortalLoginResponse(
                token,
                expiresAt,
                user.CompanyId,
                user.Company.Name,
                user.FullName,
                user.Email,
                user.Company.Status.ToString(),
                user.Company.Status == CompanyStatus.Approved),
            session,
            user.Company);
    }
}
