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

        var serviceCatalog = await GetServiceCatalogAsync(cancellationToken);
        foreach (var serviceName in plan.ServiceNames)
        {
            var applicationService = new CompanyApplicationService(application.Id, serviceName);
            var match = CompanyApplicationServiceMatcher.FindBestCandidate(serviceName, serviceCatalog);
            if (match is null)
            {
                var bestReviewCandidate = CompanyApplicationServiceMatcher.FindCandidates(serviceName, serviceCatalog).FirstOrDefault();
                applicationService.MarkForReview(bestReviewCandidate?.Score);
            }
            else if (match.ServicePrestationId.HasValue)
            {
                applicationService.MarkAsMatchedPrestation(match.ServiceId, match.ServicePrestationId.Value, match.Score);
            }
            else
            {
                applicationService.MarkAsMatched(match.ServiceId, match.Score);
            }

            db.CompanyApplicationServices.Add(applicationService);
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

    private async Task<IReadOnlyList<CompanyApplicationServiceCatalogItem>> GetServiceCatalogAsync(CancellationToken cancellationToken)
    {
        var services = await db.Services
            .AsNoTracking()
            .Where(service => service.IsActive)
            .Select(service => new
            {
                service.Id,
                service.Name,
                service.NormalizedName
            })
            .ToListAsync(cancellationToken);
        var prestations = await db.ServicePrestations
            .AsNoTracking()
            .Where(prestation => prestation.IsActive && prestation.Service!.IsActive)
            .Select(prestation => new
            {
                prestation.Id,
                prestation.Name,
                prestation.NormalizedName,
                prestation.ServiceId,
                ServiceName = prestation.Service!.Name,
                ServiceNormalizedName = prestation.Service.NormalizedName
            })
            .ToListAsync(cancellationToken);

        return services
            .Select(service => new CompanyApplicationServiceCatalogItem(
                service.Id,
                service.Name,
                CompanyApplicationServiceMatcher.Normalize(service.NormalizedName),
                null,
                null,
                null))
            .Concat(prestations.Select(prestation => new CompanyApplicationServiceCatalogItem(
                prestation.ServiceId,
                prestation.ServiceName,
                CompanyApplicationServiceMatcher.Normalize(prestation.ServiceNormalizedName),
                prestation.Id,
                prestation.Name,
                CompanyApplicationServiceMatcher.Normalize(prestation.NormalizedName))))
            .ToList();
    }
}
