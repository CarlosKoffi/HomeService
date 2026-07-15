using HomeService.Application.Abstractions;
using HomeService.Contracts.CompanyPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.CompanyPortal;

public sealed class CompanyPortalProfileManagementService(IAppDbContext db)
{
    public async Task<CompanyPortalProfileUpdateResult> UpdateCompanyInformationAsync(
        Guid companyId,
        UpdateCompanyPortalCompanyInfoRequest request,
        CancellationToken cancellationToken)
    {
        var aggregate = await LoadAggregateAsync(companyId, cancellationToken);
        if (aggregate.Company is null)
        {
            return CompanyPortalProfileUpdateResult.NotFound();
        }

        if (IsBlank(request.CompanyName) || IsBlank(request.City))
        {
            return CompanyPortalProfileUpdateResult.Invalid("La raison sociale et la ville sont obligatoires.");
        }

        aggregate.Company.UpdateCompanyInformation(
            request.CompanyName,
            request.LegalForm,
            request.RegistrationNumber,
            request.TaxIdentificationNumber,
            request.City,
            request.Address);

        aggregate.Application?.UpdateCompanyInformation(
            request.CompanyName,
            request.LegalForm,
            request.RegistrationNumber,
            request.TaxIdentificationNumber,
            request.City,
            request.Address);

        await db.SaveChangesAsync(cancellationToken);
        return CompanyPortalProfileUpdateResult.Ok();
    }

    public async Task<CompanyPortalProfileUpdateResult> UpdateContactAsync(
        Guid companyId,
        UpdateCompanyPortalContactRequest request,
        CancellationToken cancellationToken)
    {
        var aggregate = await LoadAggregateAsync(companyId, cancellationToken);
        if (aggregate.Company is null)
        {
            return CompanyPortalProfileUpdateResult.NotFound();
        }

        if (IsBlank(request.ContactName) || IsBlank(request.Email) || IsBlank(request.PhoneNumber))
        {
            return CompanyPortalProfileUpdateResult.Invalid("Le nom, l'email et le telephone sont obligatoires.");
        }

        if (!request.Email.Contains('@', StringComparison.Ordinal))
        {
            return CompanyPortalProfileUpdateResult.Invalid("L'email professionnel n'est pas valide.");
        }

        aggregate.Company.UpdateContact(request.PhoneNumber, request.Email);
        aggregate.Application?.UpdateContact(request.ContactName, request.Email, request.PhoneNumber);

        await db.SaveChangesAsync(cancellationToken);
        return CompanyPortalProfileUpdateResult.Ok();
    }

    public async Task<CompanyPortalProfileUpdateResult> UpdateOperationsAsync(
        Guid companyId,
        UpdateCompanyPortalOperationsRequest request,
        CancellationToken cancellationToken)
    {
        var aggregate = await LoadAggregateAsync(companyId, cancellationToken);
        if (aggregate.Company is null)
        {
            return CompanyPortalProfileUpdateResult.NotFound();
        }

        aggregate.Company.UpdateOperations(request.InterventionZones, request.PlannedServices);
        aggregate.Application?.UpdateOperations(request.InterventionZones, request.PlannedServices);

        await db.SaveChangesAsync(cancellationToken);
        return CompanyPortalProfileUpdateResult.Ok();
    }

    public async Task<CompanyPortalProfileUpdateResult> UpdatePaymentAsync(
        Guid companyId,
        UpdateCompanyPortalPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var aggregate = await LoadAggregateAsync(companyId, cancellationToken);
        if (aggregate.Company is null)
        {
            return CompanyPortalProfileUpdateResult.NotFound();
        }

        aggregate.Company.UpdatePayment(request.WavePaymentNumber, request.OrangeMoneyPaymentNumber);
        aggregate.Application?.UpdatePayment(request.WavePaymentNumber, request.OrangeMoneyPaymentNumber);

        await db.SaveChangesAsync(cancellationToken);
        return CompanyPortalProfileUpdateResult.Ok();
    }

    private async Task<CompanyPortalProfileAggregate> LoadAggregateAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var company = await db.Companies
            .FirstOrDefaultAsync(company => company.Id == companyId && company.Status != CompanyStatus.Suspended, cancellationToken);
        if (company is null)
        {
            return new CompanyPortalProfileAggregate(null, null);
        }

        var application = await db.CompanyApplications
            .Where(application => application.CompanyId == companyId)
            .OrderByDescending(application => application.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new CompanyPortalProfileAggregate(company, application);
    }

    private static bool IsBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value);
    }

    private sealed record CompanyPortalProfileAggregate(Company? Company, CompanyApplication? Application);
}

public sealed record CompanyPortalProfileUpdateResult(bool IsSuccess, bool IsNotFound, string? Message)
{
    public static CompanyPortalProfileUpdateResult Ok() => new(true, false, null);
    public static CompanyPortalProfileUpdateResult NotFound() => new(false, true, "Entreprise introuvable ou inactive.");
    public static CompanyPortalProfileUpdateResult Invalid(string message) => new(false, false, message);
}
