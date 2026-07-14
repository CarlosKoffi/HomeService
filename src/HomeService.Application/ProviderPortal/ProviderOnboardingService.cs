using HomeService.Application.Abstractions;
using HomeService.Contracts.ProviderPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.ProviderPortal;

public sealed class ProviderOnboardingService(IAppDbContext db)
{
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
