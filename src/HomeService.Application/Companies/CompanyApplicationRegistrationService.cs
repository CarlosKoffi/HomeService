using HomeService.Application.Abstractions;
using HomeService.Application.Security;
using HomeService.Contracts.Companies;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Companies;

public sealed class CompanyApplicationRegistrationService(IAppDbContext db)
{
    public async Task<CompanyApplicationRegistrationResult> RegisterAsync(
        RegisterCompanyRequest request,
        Guid applicationId,
        IReadOnlyList<CompanyApplicationUploadedDocument> documents,
        CancellationToken cancellationToken)
    {
        var plan = CompanyApplicationRegistrationPlanner.Build(request);
        if (plan.ValidationErrors.Count > 0)
        {
            return CompanyApplicationRegistrationResult.ValidationFailed(plan.ValidationErrors);
        }

        var existingUser = await db.CompanyPortalUsers.AnyAsync(user => user.Email == plan.Email, cancellationToken);
        if (existingUser)
        {
            return CompanyApplicationRegistrationResult.DuplicateEmail("Un compte entreprise existe deja avec cet email.");
        }

        var application = new CompanyApplication(
            request.CompanyName,
            request.RegistrationNumber,
            request.City,
            request.Address,
            request.ContactName,
            request.Email,
            request.PhoneNumber,
            plan.ServiceNames.Count > 0 ? string.Join(", ", plan.ServiceNames) : null,
            request.EstimatedProviderCount,
            applicationId);

        var company = new Company(
            request.CompanyName,
            request.PhoneNumber,
            plan.Email);
        company.UpdateCompanyInformation(
            request.CompanyName,
            null,
            request.RegistrationNumber,
            null,
            request.City,
            request.Address);
        company.UpdateOperations(null, plan.ServiceNames.Count > 0 ? string.Join(", ", plan.ServiceNames) : null);

        db.CompanyApplications.Add(application);
        db.Companies.Add(company);
        db.CompanyPortalUsers.Add(new CompanyPortalUser(company.Id, request.ContactName, plan.Email, Sha256PasswordHasher.Hash(request.Password), true));
        application.LinkPendingCompany(company.Id);
        db.CompanyApplicationStatusHistories.Add(new CompanyApplicationStatusHistory(
            application.Id,
            null,
            CompanyApplicationStatus.Submitted,
            "Compte entreprise cree. Documents de conformite en attente.",
            null));

        foreach (var serviceName in plan.ServiceNames)
        {
            db.CompanyApplicationServices.Add(new CompanyApplicationService(application.Id, serviceName));
        }

        foreach (var document in documents)
        {
            db.CompanyApplicationDocuments.Add(new CompanyApplicationDocument(
                application.Id,
                document.DocumentType,
                document.OriginalFileName,
                document.StoragePath,
                document.ContentType));
        }

        return CompanyApplicationRegistrationResult.Created(application, company, plan.ServiceNames.Count, documents.Count);
    }
}
