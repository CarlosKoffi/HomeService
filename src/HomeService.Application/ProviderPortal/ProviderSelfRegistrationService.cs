using HomeService.Application.Abstractions;
using HomeService.Application.Security;
using HomeService.Contracts.ProviderPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.ProviderPortal;

public sealed class ProviderSelfRegistrationService(IAppDbContext db)
{
    public async Task<ProviderSelfRegistrationResponse> RegisterAsync(
        ProviderSelfRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var provider = new ProviderProfile(
            request.FirstName,
            request.LastName,
            request.PhoneNumber,
            null,
            request.DateOfBirth,
            request.Address,
            ParseProviderGender(request.Gender),
            Math.Max(0, request.YearsOfExperience),
            request.Latitude,
            request.Longitude,
            Math.Clamp(request.MissionRadiusKm, 1, 100));
        provider.SetPortalPassword(Sha256PasswordHasher.Hash(request.Password));

        var requestedServiceIds = request.Services.Select(service => service.ServiceId).Distinct().ToList();
        var selectionIds = request.Selections?
            .Where(selection => selection.Id != Guid.Empty)
            .Select(selection => selection.Id)
            .Distinct()
            .ToList() ?? [];

        List<(Guid Id, Guid ServiceId)> selectedPrestations = selectionIds.Count == 0
            ? []
            : (await db.ServicePrestations
                .AsNoTracking()
                .Where(prestation => selectionIds.Contains(prestation.Id) && prestation.IsActive && prestation.Service!.IsActive)
                .Select(prestation => new { prestation.Id, prestation.ServiceId })
                .ToListAsync(cancellationToken))
                .Select(prestation => (prestation.Id, prestation.ServiceId))
                .ToList();

        requestedServiceIds.AddRange(request.Selections?
            .Where(selection => string.Equals(selection.Type, "Service", StringComparison.OrdinalIgnoreCase))
            .Select(selection => selection.Id) ?? []);
        requestedServiceIds.AddRange(selectedPrestations.Select(prestation => prestation.ServiceId));
        requestedServiceIds = requestedServiceIds.Distinct().ToList();

        var activeServiceIds = await db.Services
            .Where(service => requestedServiceIds.Contains(service.Id) && service.IsActive)
            .Select(service => service.Id)
            .ToListAsync(cancellationToken);
        var legacyCandidateServices = request.Services
            .Where(service => activeServiceIds.Contains(service.ServiceId))
            .Select(service => (
                ServiceId: service.ServiceId,
                ExperienceLevel: ParseExperienceLevel(service.ExperienceLevel),
                YearsOfExperience: Math.Max(0, service.YearsOfExperience)))
            .ToList();
        var selectionCandidateServices = request.Selections?
            .Select(selection =>
            {
                var serviceId = string.Equals(selection.Type, "Prestation", StringComparison.OrdinalIgnoreCase)
                    ? selectedPrestations
                        .Where(prestation => prestation.Id == selection.Id)
                        .Select(prestation => prestation.ServiceId)
                        .FirstOrDefault()
                    : selection.Id;

                return (
                    ServiceId: serviceId,
                    ExperienceLevel: ParseExperienceLevel(selection.ExperienceLevel),
                    YearsOfExperience: Math.Max(0, selection.YearsOfExperience));
            })
            .Where(selection => selection.ServiceId != Guid.Empty && activeServiceIds.Contains(selection.ServiceId))
            .ToList() ?? [];
        var requestedCandidateServices = legacyCandidateServices
            .Concat(selectionCandidateServices)
            .ToList();

        foreach (var proposedName in NormalizeProposedServiceNames(request.ProposedServices))
        {
            var normalizedName = proposedName.ToLowerInvariant();
            var service = await db.Services
                .FirstOrDefaultAsync(service => service.NormalizedName == normalizedName, cancellationToken);
            if (service is null)
            {
                service = new Service(proposedName, null, null);
                db.Services.Add(service);
            }

            requestedCandidateServices.Add((ServiceId: service.Id, ExperienceLevel: ExperienceLevel.Confirmed, YearsOfExperience: Math.Max(0, request.YearsOfExperience)));
        }

        var selectedOpportunityIds = request.OpportunityCompanyIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Take(5)
            .ToList() ?? [];
        var validOpportunityIds = await FindValidOpportunityCompanyIdsAsync(
            selectedOpportunityIds,
            requestedCandidateServices.Select(service => service.ServiceId).Distinct().ToList(),
            cancellationToken);

        var validationErrors = ProviderSelfRegistrationValidator.Validate(
            request,
            requestedCandidateServices.Count(),
            validOpportunityIds.Count());
        if (validationErrors.Count > 0)
        {
            return new ProviderSelfRegistrationResponse(Guid.Empty, "ValidationFailed", validationErrors[0]);
        }

        provider.SyncCandidateServices(requestedCandidateServices);

        db.Providers.Add(provider);
        var affiliationRequests = validOpportunityIds
            .Select(companyId => new ProviderAffiliationRequest(
                provider.Id,
                companyId,
                "Candidature envoyee depuis l'inscription prestataire."))
            .ToList();
        foreach (var affiliationRequest in affiliationRequests)
        {
            db.ProviderAffiliationRequests.Add(affiliationRequest);
        }

        await db.SaveChangesAsync(cancellationToken);

        return new ProviderSelfRegistrationResponse(
            provider.Id,
            provider.Status.ToString(),
            "Profil cree. Votre demande a ete envoyee aux entreprises selectionnees.",
            affiliationRequests.Select(request => request.Id).ToList());
    }

    private async Task<IReadOnlyList<Guid>> FindValidOpportunityCompanyIdsAsync(
        IReadOnlyList<Guid> companyIds,
        IReadOnlyList<Guid> serviceIds,
        CancellationToken cancellationToken)
    {
        if (companyIds.Count == 0 || serviceIds.Count == 0)
        {
            return [];
        }

        var serviceNames = await db.Services
            .AsNoTracking()
            .Where(service => serviceIds.Contains(service.Id) && service.IsActive)
            .Select(service => service.Name)
            .ToListAsync(cancellationToken);
        var providerServiceCompanyIds = await db.ProviderServices
            .AsNoTracking()
            .Where(providerService =>
                companyIds.Contains(providerService.CompanyId)
                && providerService.IsActive
                && serviceIds.Contains(providerService.ServiceId))
            .Select(providerService => providerService.CompanyId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var applicationCompanyIds = await db.CompanyApplicationServices
            .AsNoTracking()
            .Where(applicationService =>
                applicationService.MatchedServiceId != null
                && serviceIds.Contains(applicationService.MatchedServiceId.Value)
                && applicationService.CompanyApplication!.CompanyId != null
                && companyIds.Contains(applicationService.CompanyApplication.CompanyId.Value))
            .Select(applicationService => applicationService.CompanyApplication!.CompanyId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var providerServiceCompanyIdSet = providerServiceCompanyIds.ToHashSet();
        var applicationCompanyIdSet = applicationCompanyIds.ToHashSet();
        var normalizedServiceNames = serviceNames.Select(NormalizeSearch).Where(name => name.Length > 0).ToList();

        var companies = await db.Companies
            .AsNoTracking()
            .Where(company => companyIds.Contains(company.Id) && company.Status == CompanyStatus.Approved)
            .Select(company => new
            {
                company.Id,
                company.PlannedServices,
                LatestPlannedServices = db.CompanyApplications
                    .Where(application => application.CompanyId == company.Id)
                    .OrderByDescending(application => application.CreatedAt)
                    .Select(application => application.PlannedServices)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return companies
            .Where(company =>
                providerServiceCompanyIdSet.Contains(company.Id)
                || applicationCompanyIdSet.Contains(company.Id)
                || normalizedServiceNames.Any(serviceName =>
                    TextContainsService(company.PlannedServices, serviceName)
                    || TextContainsService(company.LatestPlannedServices, serviceName)))
            .Select(company => company.Id)
            .ToList();
    }

    private static ExperienceLevel ParseExperienceLevel(string? value)
    {
        return Enum.TryParse<ExperienceLevel>(value, true, out var level)
            ? level
            : ExperienceLevel.Confirmed;
    }

    private static ProviderGender ParseProviderGender(string? value)
    {
        return Enum.TryParse<ProviderGender>(value, true, out var gender)
            ? gender
            : ProviderGender.Unspecified;
    }

    private static IReadOnlyList<string> NormalizeProposedServiceNames(IEnumerable<string> services)
    {
        return services
            .Where(service => !string.IsNullOrWhiteSpace(service))
            .Select(service => service.Trim())
            .Where(service => service.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
    }

    private static bool TextContainsService(string? value, string selectedServiceName)
    {
        return !string.IsNullOrWhiteSpace(selectedServiceName)
            && NormalizeSearch(value).Contains(selectedServiceName, StringComparison.Ordinal);
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
                builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
            }
        }

        return string.Join(' ', builder
            .ToString()
            .Normalize(System.Text.NormalizationForm.FormC)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
