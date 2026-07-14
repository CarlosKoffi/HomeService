using HomeService.Application.Abstractions;
using HomeService.Application.Security;
using HomeService.Contracts.Companies;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Companies;

public sealed class CompanyActivationPreviewService(IAppDbContext db)
{
    public async Task<CompanyActivationPreviewResult> GetPreviewAsync(
        Guid applicationId,
        string token,
        CancellationToken cancellationToken)
    {
        var tokenHash = PortalTokenService.HashToken(token);
        var activationToken = await db.CompanyActivationTokens
            .AsNoTracking()
            .Where(item => item.CompanyApplicationId == applicationId && item.TokenHash == tokenHash)
            .Select(item => new
            {
                Token = item,
                item.CompanyApplication!.CompanyName,
                item.CompanyApplication.Email
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (activationToken is null || !activationToken.Token.IsActive)
        {
            return CompanyActivationPreviewResult.InvalidOrExpiredToken();
        }

        return CompanyActivationPreviewResult.Ok(new CompanyActivationPreviewResponse(
            applicationId,
            activationToken.CompanyName,
            activationToken.Email,
            activationToken.Token.ExpiresAt));
    }
}

public sealed record CompanyActivationPreviewResult(
    CompanyActivationPreviewStatus Status,
    CompanyActivationPreviewResponse? Response,
    string? Message)
{
    public static CompanyActivationPreviewResult Ok(CompanyActivationPreviewResponse response)
        => new(CompanyActivationPreviewStatus.Ok, response, null);

    public static CompanyActivationPreviewResult InvalidOrExpiredToken()
        => new(CompanyActivationPreviewStatus.InvalidOrExpiredToken, null, "Ce lien d'activation est invalide ou expire.");
}

public enum CompanyActivationPreviewStatus
{
    Ok,
    InvalidOrExpiredToken
}
