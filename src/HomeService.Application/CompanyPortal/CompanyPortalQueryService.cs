using HomeService.Application.Abstractions;
using HomeService.Contracts.CompanyPortal;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.CompanyPortal;

public sealed class CompanyPortalQueryService(IAppDbContext db)
{
    public async Task<CompanyPortalProfileResult> GetProfileAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var company = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(company => company.Id == companyId && company.Status != CompanyStatus.Suspended, cancellationToken);
        if (company is null)
        {
            return CompanyPortalProfileResult.NotFound();
        }

        var application = await db.CompanyApplications
            .AsNoTracking()
            .Where(application => application.CompanyId == companyId)
            .OrderByDescending(application => application.CreatedAt)
            .Select(application => new CompanyPortalProfileResponse(
                company.Id,
                application.Id,
                application.CompanyName,
                application.RegistrationNumber,
                application.City,
                application.Address,
                application.ContactName,
                application.Email,
                application.PhoneNumber,
                application.PlannedServices,
                application.EstimatedProviderCount,
                company.Status.ToString(),
                application.Status.ToString(),
                company.Status == CompanyStatus.Approved,
                application.ReviewNote,
                application.Documents
                    .OrderBy(document => document.DocumentType)
                    .ThenByDescending(document => document.CreatedAt)
                    .Select(document => new CompanyPortalProfileDocumentResponse(
                        document.Id,
                        document.DocumentType.ToString(),
                        GetCompanyDocumentLabel(document.DocumentType),
                        document.OriginalFileName,
                        document.ContentType,
                        document.ReviewStatus.ToString(),
                        document.ReviewNote,
                        document.CreatedAt,
                        $"/api/admin/company-application-documents/{document.Id}/download"))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return CompanyPortalProfileResult.Ok(application ?? new CompanyPortalProfileResponse(
            company.Id,
            null,
            company.Name,
            null,
            string.Empty,
            null,
            string.Empty,
            company.Email ?? string.Empty,
            company.PhoneNumber,
            null,
            null,
            company.Status.ToString(),
            "Submitted",
            company.Status == CompanyStatus.Approved,
            null,
            []));
    }

    public async Task<CompanyPortalMissionsResult> ListMissionsAsync(Guid companyId, string? view, CancellationToken cancellationToken)
    {
        if (!await CompanyExistsAsync(companyId, cancellationToken))
        {
            return CompanyPortalMissionsResult.NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        var query = from mission in db.Missions.AsNoTracking()
                    where mission.CompanyId == companyId
                    join service in db.Services.AsNoTracking() on mission.ServiceId equals service.Id
                    join customer in db.Customers.AsNoTracking() on mission.CustomerId equals customer.Id
                    join provider in db.Providers.AsNoTracking() on mission.ProviderId equals provider.Id into providerJoin
                    from provider in providerJoin.DefaultIfEmpty()
                    select new { mission, service, customer, provider };

        query = view?.Trim().ToLowerInvariant() switch
        {
            "upcoming" => query.Where(row => row.mission.ScheduledFor >= now && row.mission.Status != MissionStatus.Completed && row.mission.Status != MissionStatus.Cancelled),
            "past" => query.Where(row => row.mission.Status == MissionStatus.Completed || row.mission.Status == MissionStatus.Cancelled),
            "live" => query.Where(row => row.mission.Status == MissionStatus.SearchingProvider || row.mission.Status == MissionStatus.Offered || row.mission.Status == MissionStatus.Accepted || row.mission.Status == MissionStatus.OnTheWay || row.mission.Status == MissionStatus.Started),
            _ => query
        };

        var missions = await query
            .OrderBy(row => row.mission.ScheduledFor ?? row.mission.CreatedAt)
            .Select(row => new CompanyPortalMissionResponse(
                row.mission.Id,
                row.service.Name,
                row.customer.FirstName + " " + row.customer.LastName,
                row.customer.PhoneNumber,
                row.mission.Mode.ToString(),
                row.mission.Status.ToString(),
                row.mission.PaymentMethod.ToString(),
                row.mission.PaymentStatus.ToString(),
                row.mission.ScheduledFor,
                row.mission.EstimatedDurationMinutes,
                row.mission.FinalTotalAmount ?? row.mission.EstimatedTotalAmount,
                row.mission.Currency,
                row.mission.ProviderId,
                row.provider == null ? null : row.provider.FirstName + " " + row.provider.LastName))
            .ToListAsync(cancellationToken);

        return CompanyPortalMissionsResult.Ok(missions);
    }

    public async Task<CompanyPortalPaymentsResult> GetPaymentsAsync(Guid companyId, string? period, CancellationToken cancellationToken)
    {
        if (!await CompanyExistsAsync(companyId, cancellationToken))
        {
            return CompanyPortalPaymentsResult.NotFound();
        }

        var normalizedPeriod = period?.Trim().ToLowerInvariant() ?? "month";
        var start = PaymentPeriodCalculator.GetStart(normalizedPeriod, DateTimeOffset.UtcNow);
        var missions = await (from mission in db.Missions.AsNoTracking()
                              where mission.CompanyId == companyId
                                  && mission.Status == MissionStatus.Completed
                                  && (mission.ScheduledFor == null || mission.ScheduledFor >= start)
                              join service in db.Services.AsNoTracking() on mission.ServiceId equals service.Id
                              join customer in db.Customers.AsNoTracking() on mission.CustomerId equals customer.Id
                              join provider in db.Providers.AsNoTracking() on mission.ProviderId equals provider.Id into providerJoin
                              from provider in providerJoin.DefaultIfEmpty()
                              orderby mission.ScheduledFor descending
                              select new CompanyPortalMissionResponse(
                                  mission.Id,
                                  service.Name,
                                  customer.FirstName + " " + customer.LastName,
                                  customer.PhoneNumber,
                                  mission.Mode.ToString(),
                                  mission.Status.ToString(),
                                  mission.PaymentMethod.ToString(),
                                  mission.PaymentStatus.ToString(),
                                  mission.ScheduledFor,
                                  mission.EstimatedDurationMinutes,
                                  mission.FinalTotalAmount ?? mission.EstimatedTotalAmount,
                                  mission.Currency,
                                  mission.ProviderId,
                                  provider == null ? null : provider.FirstName + " " + provider.LastName))
            .ToListAsync(cancellationToken);

        return CompanyPortalPaymentsResult.Ok(new CompanyPortalPaymentSummaryResponse(
            normalizedPeriod,
            missions.Sum(mission => mission.FinalTotalAmount ?? 0),
            missions.Where(mission => mission.PaymentMethod == PaymentMethod.MobileMoney.ToString()).Sum(mission => mission.FinalTotalAmount ?? 0),
            missions.Where(mission => mission.PaymentMethod == PaymentMethod.Cash.ToString()).Sum(mission => mission.FinalTotalAmount ?? 0),
            missions.Where(mission => mission.PaymentMethod == PaymentMethod.Cash.ToString()).Sum(mission => mission.FinalTotalAmount ?? 0),
            missions.Count,
            "XOF",
            missions));
    }

    private async Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken)
    {
        return await db.Companies.AnyAsync(company => company.Id == companyId && company.Status != CompanyStatus.Suspended, cancellationToken);
    }

    private static string GetCompanyDocumentLabel(CompanyDocumentType documentType)
    {
        return documentType switch
        {
            CompanyDocumentType.FiscalExistenceDeclaration => "DFE",
            CompanyDocumentType.BusinessRegistration => "Registre de commerce",
            CompanyDocumentType.OwnerIdentity => "Identite du responsable",
            CompanyDocumentType.AddressProof => "Justificatif d'adresse",
            _ => "Document complementaire"
        };
    }
}

public sealed record CompanyPortalProfileResult(bool IsSuccess, CompanyPortalProfileResponse? Response, string? Message)
{
    public static CompanyPortalProfileResult Ok(CompanyPortalProfileResponse response) => new(true, response, null);
    public static CompanyPortalProfileResult NotFound() => new(false, null, "Entreprise introuvable ou inactive.");
}

public sealed record CompanyPortalMissionsResult(bool IsSuccess, IReadOnlyList<CompanyPortalMissionResponse> Missions, string? Message)
{
    public static CompanyPortalMissionsResult Ok(IReadOnlyList<CompanyPortalMissionResponse> missions) => new(true, missions, null);
    public static CompanyPortalMissionsResult NotFound() => new(false, [], "Entreprise introuvable ou inactive.");
}

public sealed record CompanyPortalPaymentsResult(bool IsSuccess, CompanyPortalPaymentSummaryResponse? Summary, string? Message)
{
    public static CompanyPortalPaymentsResult Ok(CompanyPortalPaymentSummaryResponse summary) => new(true, summary, null);
    public static CompanyPortalPaymentsResult NotFound() => new(false, null, "Entreprise introuvable ou inactive.");
}
