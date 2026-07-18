using HomeService.Application.Abstractions;
using HomeService.Contracts.ProviderPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.ProviderPortal;

public sealed class ProviderOnboardingService(IAppDbContext db)
{
    public async Task<IReadOnlyList<ProviderOnboardingOptionResponse>> SearchOptionsAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        var normalizedSearch = NormalizeSearch(search);
        var services = await db.Services
            .AsNoTracking()
            .Where(service => service.IsActive)
            .OrderBy(service => service.Name)
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
            .OrderBy(prestation => prestation.Service!.Name)
            .ThenBy(prestation => prestation.SortOrder)
            .ThenBy(prestation => prestation.Name)
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

        var serviceOptions = services
            .Where(service => MatchesSearch(service.Name, normalizedSearch))
            .Select(service => new ProviderOnboardingOptionResponse(
                service.Id,
                "Service",
                service.Name,
                service.Id,
                service.Name,
                null,
                null));
        var prestationOptions = prestations
            .Where(prestation => MatchesSearch(prestation.Name, normalizedSearch)
                || MatchesSearch(prestation.ServiceName, normalizedSearch)
                || MatchesSearch($"{prestation.ServiceName} {prestation.Name}", normalizedSearch))
            .Select(prestation => new ProviderOnboardingOptionResponse(
                prestation.Id,
                "Prestation",
                $"{prestation.ServiceName} - {prestation.Name}",
                prestation.ServiceId,
                prestation.ServiceName,
                prestation.Id,
                prestation.Name));

        return serviceOptions
            .Concat(prestationOptions)
            .OrderBy(option => option.ServiceName)
            .ThenBy(option => option.Type)
            .ThenBy(option => option.Label)
            .Take(50)
            .ToList();
    }

    public async Task<IReadOnlyList<ProviderCompanyOpportunityResponse>> SearchOpportunitiesAsync(
        string? selectionType,
        Guid selectionId,
        string? address,
        CancellationToken cancellationToken)
    {
        if (selectionId == Guid.Empty)
        {
            return [];
        }

        var serviceId = selectionId;
        Guid? prestationId = null;
        var selectedService = await db.Services
            .AsNoTracking()
            .Where(service => service.Id == serviceId && service.IsActive)
            .Select(service => new { service.Id, service.Name })
            .FirstOrDefaultAsync(cancellationToken);
        if (string.Equals(selectionType, "Prestation", StringComparison.OrdinalIgnoreCase))
        {
            var prestation = await db.ServicePrestations
                .AsNoTracking()
                .Where(prestation => prestation.Id == selectionId && prestation.IsActive && prestation.Service!.IsActive)
                .Select(prestation => new { prestation.Id, prestation.ServiceId, prestation.Name, ServiceName = prestation.Service!.Name })
                .FirstOrDefaultAsync(cancellationToken);
            if (prestation is null)
            {
                return [];
            }

            serviceId = prestation.ServiceId;
            prestationId = prestation.Id;
            selectedService = new { Id = prestation.ServiceId, Name = prestation.ServiceName };
        }

        if (selectedService is null)
        {
            return [];
        }

        var normalizedAddress = NormalizeSearch(address);
        var companyIdsWithProviderService = await db.ProviderServices
            .AsNoTracking()
            .Where(providerService =>
                providerService.IsActive
                && providerService.ServiceId == serviceId)
            .Select(providerService => providerService.CompanyId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var companyIdsWithExactPrestation = prestationId is null
            ? []
            : await db.ProviderServicePrestations
                .AsNoTracking()
                .Where(providerPrestation =>
                    providerPrestation.IsActive
                    && providerPrestation.ServicePrestationId == prestationId
                    && providerPrestation.ProviderService!.IsActive)
                .Select(providerPrestation => providerPrestation.ProviderService!.CompanyId)
                .Distinct()
                .ToListAsync(cancellationToken);
        var companyIdsFromApplications = await db.CompanyApplicationServices
            .AsNoTracking()
            .Where(applicationService =>
                applicationService.MatchedServiceId == serviceId
                && applicationService.CompanyApplication!.CompanyId != null)
            .Select(applicationService => applicationService.CompanyApplication!.CompanyId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var rows = await db.Companies
            .AsNoTracking()
            .Where(company => company.Status == CompanyStatus.Approved)
            .Select(company => new
            {
                Company = company,
                LatestApplication = db.CompanyApplications
                    .Where(application => application.CompanyId == company.Id)
                    .OrderByDescending(application => application.CreatedAt)
                    .FirstOrDefault(),
                Services = db.ProviderServices
                    .Where(providerService =>
                        providerService.CompanyId == company.Id
                        && providerService.IsActive
                        && providerService.ServiceId == serviceId)
                    .Select(providerService => providerService.Service!.Name)
                    .Distinct()
                    .OrderBy(name => name)
                    .ToList(),
                Prestations = db.ProviderServicePrestations
                    .Where(providerPrestation =>
                        providerPrestation.IsActive
                        && providerPrestation.ServicePrestation!.ServiceId == serviceId
                        && (prestationId == null || providerPrestation.ServicePrestationId == prestationId)
                        && db.ProviderServices.Any(providerService =>
                            providerService.Id == providerPrestation.ProviderServiceId
                            && providerService.CompanyId == company.Id
                            && providerService.IsActive))
                    .Select(providerPrestation => providerPrestation.ServicePrestation!.Name)
                    .Distinct()
                    .OrderBy(name => name)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var providerServiceCompanyIds = companyIdsWithProviderService.ToHashSet();
        var exactPrestationCompanyIds = companyIdsWithExactPrestation.ToHashSet();
        var applicationCompanyIds = companyIdsFromApplications.ToHashSet();
        var selectedServiceName = NormalizeSearch(selectedService.Name);

        var matchedRows = rows
            .Where(row => CompanyMatchesRequestedWork(
                row.Company,
                row.LatestApplication,
                providerServiceCompanyIds,
                exactPrestationCompanyIds,
                applicationCompanyIds,
                selectedServiceName))
            .Select(row =>
            {
                var city = row.Company.City ?? row.LatestApplication?.City;
                var companyAddress = row.Company.Address ?? row.LatestApplication?.Address;
                var proximityScore = ComputeProximityScore(normalizedAddress, city, companyAddress, row.Company.InterventionZones);
                return new
                {
                    row,
                    proximityScore,
                    ProximityLabel = BuildProximityLabel(proximityScore, city, companyAddress)
                };
            })
            .ToList();

        var rowsToDisplay = matchedRows.Count(row => row.proximityScore > 0) >= 3
            ? matchedRows.Where(row => row.proximityScore > 0)
            : matchedRows;

        return rowsToDisplay
            .OrderByDescending(row => row.proximityScore)
            .ThenBy(row => row.row.Company.Name)
            .Take(12)
            .Select(row => new ProviderCompanyOpportunityResponse(
                row.row.Company.Id,
                row.row.Company.Name,
                row.row.Company.City ?? row.row.LatestApplication?.City,
                row.row.Company.Address ?? row.row.LatestApplication?.Address,
                row.row.Services,
                row.row.Prestations,
                row.ProximityLabel,
                row.row.Company.Status.ToString()))
            .ToList();
    }

    public async Task<IReadOnlyList<ProviderCompanySearchResponse>> SearchCompaniesAsync(
        string? serviceIds,
        CancellationToken cancellationToken)
    {
        var requestedServiceIds = ParseGuidList(serviceIds);
        if (requestedServiceIds.Count == 0)
        {
            return [];
        }

        return await db.Companies
            .AsNoTracking()
            .Where(company => company.Status == CompanyStatus.Approved)
            .Select(company => new
            {
                Company = company,
                LatestApplication = db.CompanyApplications
                    .Where(application => application.CompanyId == company.Id)
                    .OrderByDescending(application => application.CreatedAt)
                    .FirstOrDefault(),
                MatchingServices = db.ProviderServices
                    .Where(providerService =>
                        providerService.CompanyId == company.Id
                        && providerService.IsActive
                        && requestedServiceIds.Contains(providerService.ServiceId))
                    .Select(providerService => providerService.Service!.Name)
                    .Distinct()
                    .OrderBy(name => name)
                    .ToList()
            })
            .Where(row => row.MatchingServices.Count > 0)
            .OrderByDescending(row => row.MatchingServices.Count)
            .ThenBy(row => row.Company.Name)
            .Take(30)
            .Select(row => new ProviderCompanySearchResponse(
                row.Company.Id,
                row.Company.Name,
                row.LatestApplication == null ? null : row.LatestApplication.City,
                row.MatchingServices.Count,
                row.MatchingServices,
                null,
                row.Company.Status.ToString()))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProviderAffiliationRequestResult> CreateAffiliationRequestAsync(
        ProviderAffiliationRequestCreateRequest request,
        CancellationToken cancellationToken)
    {
        var provider = await db.Providers.FirstOrDefaultAsync(provider => provider.Id == request.ProviderId, cancellationToken);
        var providerEligibility = ProviderAffiliationRequestPolicy.EvaluateProvider(provider);
        if (!providerEligibility.IsSuccess)
        {
            return providerEligibility.ToResult();
        }

        var company = await db.Companies.FirstOrDefaultAsync(
            company => company.Id == request.CompanyId && company.Status == CompanyStatus.Approved,
            cancellationToken);
        var hasPending = await db.ProviderAffiliationRequests.AnyAsync(existing =>
            existing.ProviderId == request.ProviderId
            && existing.CompanyId == request.CompanyId
            && existing.Status == ProviderAffiliationRequestStatus.Pending,
            cancellationToken);
        var companyEligibility = ProviderAffiliationRequestPolicy.EvaluateCompany(company, hasPending);
        if (!companyEligibility.IsSuccess)
        {
            return companyEligibility.ToResult();
        }

        var affiliationRequest = new ProviderAffiliationRequest(request.ProviderId, request.CompanyId, request.Message);
        db.ProviderAffiliationRequests.Add(affiliationRequest);
        await db.SaveChangesAsync(cancellationToken);

        return ProviderAffiliationRequestResult.Ok(new ProviderAffiliationRequestResponse(
            affiliationRequest.Id,
            affiliationRequest.ProviderId,
            affiliationRequest.CompanyId,
            company!.Name,
            affiliationRequest.Status.ToString(),
            affiliationRequest.RequestedAt,
            "Demande envoyee. L'entreprise doit vous recevoir, vous evaluer et valider votre profil avant toute mission."));
    }

    private static IReadOnlyList<Guid> ParseGuidList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => Guid.TryParse(item, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
    }

    private static bool MatchesSearch(string value, string normalizedSearch)
    {
        return string.IsNullOrWhiteSpace(normalizedSearch)
            || NormalizeSearch(value).Contains(normalizedSearch, StringComparison.Ordinal);
    }

    private static bool CompanyMatchesRequestedWork(
        Company company,
        CompanyApplication? latestApplication,
        IReadOnlySet<Guid> providerServiceCompanyIds,
        IReadOnlySet<Guid> exactPrestationCompanyIds,
        IReadOnlySet<Guid> applicationCompanyIds,
        string selectedServiceName)
    {
        if (providerServiceCompanyIds.Contains(company.Id)
            || exactPrestationCompanyIds.Contains(company.Id)
            || applicationCompanyIds.Contains(company.Id))
        {
            return true;
        }

        return TextContainsService(company.PlannedServices, selectedServiceName)
            || TextContainsService(latestApplication?.PlannedServices, selectedServiceName);
    }

    private static bool TextContainsService(string? value, string selectedServiceName)
    {
        return !string.IsNullOrWhiteSpace(selectedServiceName)
            && NormalizeSearch(value).Contains(selectedServiceName, StringComparison.Ordinal);
    }

    private static int ComputeProximityScore(string normalizedAddress, params string?[] companyLocations)
    {
        if (string.IsNullOrWhiteSpace(normalizedAddress))
        {
            return 0;
        }

        var tokens = normalizedAddress
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 3)
            .Distinct()
            .ToList();
        if (tokens.Count == 0)
        {
            return 0;
        }

        var normalizedLocations = companyLocations
            .Where(location => !string.IsNullOrWhiteSpace(location))
            .Select(NormalizeSearch)
            .ToList();

        return tokens.Count(token => normalizedLocations.Any(location => location.Contains(token, StringComparison.Ordinal)));
    }

    private static string BuildProximityLabel(int proximityScore, string? city, string? address)
    {
        if (proximityScore > 0)
        {
            return "Proche de votre zone";
        }

        if (!string.IsNullOrWhiteSpace(address))
        {
            return address;
        }

        return string.IsNullOrWhiteSpace(city) ? "Zone a confirmer" : city;
    }

    private static string NormalizeSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character);
            if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }
}

public sealed record ProviderAffiliationRequestResult(
    ProviderAffiliationRequestStatusCode Status,
    string Message,
    ProviderAffiliationRequestResponse? Response)
{
    public static ProviderAffiliationRequestResult Ok(ProviderAffiliationRequestResponse response)
        => new(ProviderAffiliationRequestStatusCode.Success, response.Message, response);

    public static ProviderAffiliationRequestResult NotFound(string message)
        => new(ProviderAffiliationRequestStatusCode.NotFound, message, null);

    public static ProviderAffiliationRequestResult ValidationFailed(string message)
        => new(ProviderAffiliationRequestStatusCode.ValidationFailed, message, null);
}

public enum ProviderAffiliationRequestStatusCode
{
    Success,
    NotFound,
    ValidationFailed
}

public static class ProviderAffiliationRequestPolicy
{
    public static ProviderAffiliationRequestEligibility EvaluateProvider(ProviderProfile? provider)
    {
        if (provider is null)
        {
            return ProviderAffiliationRequestEligibility.NotFound("Profil prestataire introuvable.");
        }

        if (provider.CompanyId is not null || provider.Status != ProviderStatus.InterimCandidate)
        {
            return ProviderAffiliationRequestEligibility.ValidationFailed("Ce profil n'est pas en statut demandeur d'interim.");
        }

        return ProviderAffiliationRequestEligibility.Success();
    }

    public static ProviderAffiliationRequestEligibility EvaluateCompany(Company? company, bool hasPendingRequest)
    {
        if (company is null)
        {
            return ProviderAffiliationRequestEligibility.NotFound("Entreprise introuvable ou non active.");
        }

        if (hasPendingRequest)
        {
            return ProviderAffiliationRequestEligibility.ValidationFailed("Une demande est deja en attente pour cette entreprise.");
        }

        return ProviderAffiliationRequestEligibility.Success();
    }
}

public sealed record ProviderAffiliationRequestEligibility(
    ProviderAffiliationRequestStatusCode Status,
    string Message)
{
    public bool IsSuccess => Status == ProviderAffiliationRequestStatusCode.Success;

    public static ProviderAffiliationRequestEligibility Success()
        => new(ProviderAffiliationRequestStatusCode.Success, string.Empty);

    public static ProviderAffiliationRequestEligibility NotFound(string message)
        => new(ProviderAffiliationRequestStatusCode.NotFound, message);

    public static ProviderAffiliationRequestEligibility ValidationFailed(string message)
        => new(ProviderAffiliationRequestStatusCode.ValidationFailed, message);

    public ProviderAffiliationRequestResult ToResult()
        => Status switch
        {
            ProviderAffiliationRequestStatusCode.NotFound => ProviderAffiliationRequestResult.NotFound(Message),
            ProviderAffiliationRequestStatusCode.ValidationFailed => ProviderAffiliationRequestResult.ValidationFailed(Message),
            _ => throw new InvalidOperationException("A successful eligibility result cannot be converted to an error result.")
        };
}
