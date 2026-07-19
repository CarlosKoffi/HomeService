using HomeService.Application.Abstractions;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminCompanyOperationsService(IAppDbContext db)
{
    public async Task<AdminCompanyOperationResult> SuspendAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var company = await db.Companies.FirstOrDefaultAsync(company => company.Id == companyId, cancellationToken);
        if (company is null)
        {
            return AdminCompanyOperationResult.NotFound();
        }

        if (company.Status == CompanyStatus.Suspended)
        {
            return AdminCompanyOperationResult.ValidationFailed(company, "Cette entreprise est deja suspendue.");
        }

        var previousStatus = company.Status;
        company.Suspend();
        return AdminCompanyOperationResult.Ok(company, previousStatus);
    }

    public async Task<AdminCompanyOperationResult> ReactivateAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var company = await db.Companies.FirstOrDefaultAsync(company => company.Id == companyId, cancellationToken);
        if (company is null)
        {
            return AdminCompanyOperationResult.NotFound();
        }

        if (company.Status != CompanyStatus.Suspended)
        {
            return AdminCompanyOperationResult.ValidationFailed(company, "Seule une entreprise suspendue peut etre reactivee.");
        }

        var previousStatus = company.Status;
        company.Approve();
        return AdminCompanyOperationResult.Ok(company, previousStatus);
    }
}

public sealed record AdminCompanyOperationResult(
    AdminCompanyOperationStatus Status,
    Company? Company,
    CompanyStatus? PreviousStatus,
    string? Message)
{
    public static AdminCompanyOperationResult Ok(Company company, CompanyStatus previousStatus)
        => new(AdminCompanyOperationStatus.Ok, company, previousStatus, null);

    public static AdminCompanyOperationResult NotFound()
        => new(AdminCompanyOperationStatus.NotFound, null, null, "Entreprise introuvable.");

    public static AdminCompanyOperationResult ValidationFailed(Company company, string message)
        => new(AdminCompanyOperationStatus.ValidationFailed, company, company.Status, message);
}

public enum AdminCompanyOperationStatus
{
    Ok = 0,
    NotFound = 1,
    ValidationFailed = 2
}
