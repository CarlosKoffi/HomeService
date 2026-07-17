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
                application.RegistrationNumber ?? company.RegistrationNumber,
                application.LegalForm ?? company.LegalForm,
                application.TaxIdentificationNumber ?? company.TaxIdentificationNumber,
                application.City,
                application.Address ?? company.Address,
                application.ContactName,
                application.Email,
                application.PhoneNumber,
                application.PlannedServices ?? company.PlannedServices,
                application.InterventionZones ?? company.InterventionZones,
                application.WavePaymentNumber ?? company.WavePaymentNumber,
                application.OrangeMoneyPaymentNumber ?? company.OrangeMoneyPaymentNumber,
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
            company.RegistrationNumber,
            company.LegalForm,
            company.TaxIdentificationNumber,
            company.City ?? string.Empty,
            company.Address,
            string.Empty,
            company.Email ?? string.Empty,
            company.PhoneNumber,
            company.PlannedServices,
            company.InterventionZones,
            company.WavePaymentNumber,
            company.OrangeMoneyPaymentNumber,
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
                row.provider == null ? null : row.provider.FirstName + " " + row.provider.LastName,
                row.mission.CompanyQuotedAmount,
                row.mission.CompanyQuoteJustification,
                row.mission.CompanyQuotedAt,
                row.mission.CustomerQuoteAcceptedAt,
                row.service.IconName,
                row.mission.ServiceAddress,
                row.mission.ActualDurationMinutes,
                null,
                row.mission.Status == MissionStatus.Cancelled
                    ? (row.mission.CancellationFeeAmount > 0 ? "Annulation apres confirmation client" : "Annulation sans frais")
                    : null))
            .ToListAsync(cancellationToken);

        return CompanyPortalMissionsResult.Ok(missions);
    }

    public async Task<CompanyPortalEmployeesResult> ListEmployeesAsync(Guid companyId, CancellationToken cancellationToken)
    {
        if (!await CompanyExistsAsync(companyId, cancellationToken))
        {
            return CompanyPortalEmployeesResult.NotFound();
        }

        var employees = await db.Providers
            .AsNoTracking()
            .Where(provider => provider.CompanyId == companyId && provider.Status != ProviderStatus.Inactive)
            .OrderBy(provider => provider.LastName)
            .ThenBy(provider => provider.FirstName)
            .Select(provider => new CompanyEmployeeResponse(
                provider.Id,
                provider.FirstName,
                provider.LastName,
                provider.PhoneNumber,
                provider.Email,
                provider.DateOfBirth,
                provider.Address,
                provider.Gender.ToString(),
                provider.EmploymentType.ToString(),
                provider.EmploymentType == ProviderEmploymentType.TemporaryWorker,
                provider.YearsOfExperience,
                provider.Status.ToString(),
                provider.IsAvailable,
                provider.MissionLatitude ?? provider.CurrentLatitude,
                provider.MissionLongitude ?? provider.CurrentLongitude,
                provider.MissionRadiusKm,
                provider.Documents
                    .Where(document => document.DocumentType == ProviderDocumentType.Photo)
                    .OrderByDescending(document => document.CreatedAt)
                    .Select(document => $"/api/company-portal/provider-documents/{document.Id}/preview")
                    .FirstOrDefault(),
                provider.Documents
                    .Where(document => document.DocumentType == ProviderDocumentType.IdentityDocument)
                    .OrderByDescending(document => document.CreatedAt)
                    .Select(document => $"/api/company-portal/provider-documents/{document.Id}/preview")
                    .FirstOrDefault(),
                provider.Documents
                    .Where(document => document.DocumentType == ProviderDocumentType.Diploma)
                    .OrderByDescending(document => document.CreatedAt)
                    .Select(document => $"/api/company-portal/provider-documents/{document.Id}/preview")
                    .FirstOrDefault(),
                provider.Documents.Any(document => document.DocumentType == ProviderDocumentType.Diploma),
                provider.Services
                    .Where(providerService => providerService.IsActive)
                    .OrderBy(providerService => providerService.Service!.Name)
                    .Select(providerService => new CompanyEmployeeServiceResponse(
                        providerService.ServiceId,
                        providerService.Service!.Name,
                        providerService.ExperienceLevel.ToString(),
                        providerService.YearsOfExperience,
                        providerService.PriceTier.ToString(),
                        providerService.Service.NormalPriceAmount,
                        providerService.Service.PremiumPriceAmount,
                        providerService.Service.Currency,
                        providerService.IsActive,
                        providerService.Prestations
                            .Where(prestation => prestation.IsActive)
                            .OrderBy(prestation => prestation.ServicePrestation!.SortOrder)
                            .ThenBy(prestation => prestation.ServicePrestation!.Name)
                            .Select(prestation => new CompanyEmployeeServicePrestationResponse(
                                prestation.ServicePrestationId,
                                prestation.ServicePrestation!.Name,
                                prestation.ServicePrestation.NormalPriceAmount,
                                prestation.ServicePrestation.PremiumPriceAmount,
                                prestation.ServicePrestation.Currency,
                                prestation.IsActive,
                                prestation.ServicePrestation.PriceMinAmount,
                                prestation.ServicePrestation.PriceMaxAmount))
                            .ToList(),
                        providerService.Service.PriceMinAmount,
                        providerService.Service.PriceMaxAmount))
                    .ToList(),
                provider.Documents
                    .OrderBy(document => document.DocumentType)
                    .ThenByDescending(document => document.CreatedAt)
                    .Select(document => new CompanyEmployeeDocumentResponse(
                        document.Id,
                        document.DocumentType.ToString(),
                        document.OriginalFileName,
                        document.ContentType,
                        $"/api/company-portal/provider-documents/{document.Id}/preview",
                        document.CreatedAt))
                    .ToList(),
                0,
                null,
                provider.CreatedAt,
                db.ProviderInvitations
                    .Where(invitation => invitation.ProviderId == provider.Id && invitation.Status == ProviderInvitationStatus.Pending)
                    .OrderByDescending(invitation => invitation.CreatedAt)
                    .Select(invitation => invitation.Code)
                    .FirstOrDefault(),
                db.ProviderInvitations
                    .Where(invitation => invitation.ProviderId == provider.Id && invitation.Status == ProviderInvitationStatus.Pending)
                    .OrderByDescending(invitation => invitation.CreatedAt)
                    .Select(invitation => invitation.InvitationLink)
                    .FirstOrDefault(),
                db.ProviderInvitations
                    .Where(invitation => invitation.ProviderId == provider.Id && invitation.Status == ProviderInvitationStatus.Pending)
                    .OrderByDescending(invitation => invitation.CreatedAt)
                    .Select(invitation => (DateTimeOffset?)invitation.ExpiresAt)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        if (employees.Count == 0)
        {
            return CompanyPortalEmployeesResult.Ok(employees);
        }

        var providerIds = employees.Select(employee => employee.Id).ToHashSet();
        var missionRows = await (from mission in db.Missions.AsNoTracking()
                                 where mission.CompanyId == companyId
                                     && mission.ProviderId != null
                                     && providerIds.Contains(mission.ProviderId.Value)
                                 join service in db.Services.AsNoTracking() on mission.ServiceId equals service.Id
                                 join customer in db.Customers.AsNoTracking() on mission.CustomerId equals customer.Id
                                 select new EmployeeMissionRow(
                                     mission.ProviderId!.Value,
                                     mission.Id,
                                     service.Name,
                                     customer.FirstName + " " + customer.LastName,
                                     mission.ServiceAddress,
                                     mission.ScheduledFor,
                                     mission.Status,
                                     mission.CreatedAt))
            .ToListAsync(cancellationToken);

        var completedMissionCounts = missionRows
            .Where(mission => mission.Status == MissionStatus.Completed)
            .GroupBy(mission => mission.ProviderId)
            .ToDictionary(group => group.Key, group => group.Count());

        var currentMissions = missionRows
            .Where(mission => IsCurrentMissionStatus(mission.Status))
            .OrderBy(mission => mission.ScheduledFor ?? mission.CreatedAt)
            .GroupBy(mission => mission.ProviderId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var mission = group.First();
                    return new CompanyEmployeeCurrentMissionResponse(
                        mission.MissionId,
                        mission.ServiceName,
                        mission.CustomerName,
                        mission.LocationLabel,
                        mission.ScheduledFor,
                        mission.Status.ToString());
                });

        employees = employees
            .Select(employee => employee with
            {
                CompletedMissionCount = completedMissionCounts.GetValueOrDefault(employee.Id),
                CurrentMission = currentMissions.GetValueOrDefault(employee.Id),
                IsAvailable = employee.IsAvailable && !currentMissions.ContainsKey(employee.Id)
            })
            .ToList();

        return CompanyPortalEmployeesResult.Ok(employees);
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
                                  provider == null ? null : provider.FirstName + " " + provider.LastName,
                                  mission.CompanyQuotedAmount,
                                  mission.CompanyQuoteJustification,
                                  mission.CompanyQuotedAt,
                                  mission.CustomerQuoteAcceptedAt,
                                  service.IconName,
                                  mission.ServiceAddress,
                                  mission.ActualDurationMinutes,
                                  null,
                                  mission.Status == MissionStatus.Cancelled
                                      ? (mission.CancellationFeeAmount > 0 ? "Annulation apres confirmation client" : "Annulation sans frais")
                                      : null))
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

    private static bool IsCurrentMissionStatus(MissionStatus status)
    {
        return status is MissionStatus.Assigned
            or MissionStatus.Accepted
            or MissionStatus.OnTheWay
            or MissionStatus.Started;
    }
}

internal sealed record EmployeeMissionRow(
    Guid ProviderId,
    Guid MissionId,
    string ServiceName,
    string CustomerName,
    string? LocationLabel,
    DateTimeOffset? ScheduledFor,
    MissionStatus Status,
    DateTimeOffset CreatedAt);

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

public sealed record CompanyPortalEmployeesResult(bool IsSuccess, IReadOnlyList<CompanyEmployeeResponse> Employees, string? Message)
{
    public static CompanyPortalEmployeesResult Ok(IReadOnlyList<CompanyEmployeeResponse> employees) => new(true, employees, null);
    public static CompanyPortalEmployeesResult NotFound() => new(false, [], "Entreprise introuvable ou inactive.");
}

public sealed record CompanyPortalPaymentsResult(bool IsSuccess, CompanyPortalPaymentSummaryResponse? Summary, string? Message)
{
    public static CompanyPortalPaymentsResult Ok(CompanyPortalPaymentSummaryResponse summary) => new(true, summary, null);
    public static CompanyPortalPaymentsResult NotFound() => new(false, null, "Entreprise introuvable ou inactive.");
}
