using HomeService.Application.Abstractions;
using HomeService.Application.Security;
using HomeService.Contracts.Companies;
using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Companies;

public sealed class CompanyActivationPasswordService(IAppDbContext db)
{
    public async Task<CompanyActivationPasswordResult> CreatePasswordAsync(
        CompanyActivationPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = CompanyActivationPasswordValidator.Validate(request);
        if (validationError is not null)
        {
            return CompanyActivationPasswordResult.ValidationFailed(validationError);
        }

        var tokenHash = PortalTokenService.HashToken(request.Token);
        var activationToken = await db.CompanyActivationTokens
            .Include(token => token.CompanyApplication)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (activationToken is null || !activationToken.IsActive || activationToken.CompanyApplication is null)
        {
            return CompanyActivationPasswordResult.InvalidOrExpiredToken();
        }

        var application = activationToken.CompanyApplication;
        var email = application.Email.ToLowerInvariant();
        var existingUser = await db.CompanyPortalUsers.AnyAsync(user => user.Email == email, cancellationToken);
        if (existingUser)
        {
            return CompanyActivationPasswordResult.DuplicatePortalUser();
        }

        Company company;
        if (application.CompanyId is { } companyId)
        {
            company = await db.Companies.FirstAsync(company => company.Id == companyId, cancellationToken);
        }
        else
        {
            company = new Company(application.CompanyName, application.PhoneNumber, application.Email);
            company.Approve();
            db.Companies.Add(company);
            application.LinkApprovedCompany(company.Id);
        }

        db.CompanyPortalUsers.Add(new CompanyPortalUser(company.Id, application.ContactName, email, Sha256PasswordHasher.Hash(request.Password), true));
        activationToken.MarkUsed();
        var previousStatus = application.Status;
        application.MarkActivated("activation");
        AddStatusHistory(application.Id, previousStatus, application.Status);

        return CompanyActivationPasswordResult.Ok(
            new CompanyActivationPasswordResponse(true, "Mot de passe cree. Votre portail entreprise est pret."),
            application,
            company,
            previousStatus,
            email);
    }

    private void AddStatusHistory(Guid companyApplicationId, Domain.Enums.CompanyApplicationStatus previousStatus, Domain.Enums.CompanyApplicationStatus newStatus)
    {
        if (previousStatus == newStatus)
        {
            return;
        }

        db.CompanyApplicationStatusHistories.Add(new CompanyApplicationStatusHistory(
            companyApplicationId,
            previousStatus,
            newStatus,
            "Compte entreprise active.",
            "activation"));
    }
}
