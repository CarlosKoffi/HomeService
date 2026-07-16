using HomeService.Application.Abstractions;
using HomeService.Contracts.CompanyPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.CompanyPortal;

public sealed class CompanyEmployeeManagementService(IAppDbContext db)
{
    public async Task<CompanyEmployeeOperationResult> UpdateProfileAsync(
        Guid companyId,
        Guid employeeId,
        UpdateCompanyEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var provider = await db.Providers.FirstOrDefaultAsync(provider => provider.Id == employeeId && provider.CompanyId == companyId, cancellationToken);
        if (provider is null)
        {
            return CompanyEmployeeOperationResult.NotFound();
        }

        var before = SnapshotProfile(provider);
        provider.UpdateCompanyProfile(
            request.FirstName,
            request.LastName,
            request.PhoneNumber,
            request.Email,
            request.DateOfBirth,
            request.Address,
            ParseProviderGender(request.Gender),
            ParseProviderEmploymentType(request.EmploymentType),
            request.YearsOfExperience,
            request.MissionLatitude,
            request.MissionLongitude,
            request.MissionRadiusKm);

        return CompanyEmployeeOperationResult.Ok(provider, before, SnapshotProfile(provider));
    }

    public async Task<CompanyEmployeeOperationResult> UpdateServicesAsync(
        Guid companyId,
        Guid employeeId,
        UpdateCompanyEmployeeServicesRequest request,
        CancellationToken cancellationToken)
    {
        var provider = await db.Providers
            .Include(provider => provider.Services)
                .ThenInclude(providerService => providerService.Prestations)
            .FirstOrDefaultAsync(provider => provider.Id == employeeId && provider.CompanyId == companyId, cancellationToken);
        if (provider is null)
        {
            return CompanyEmployeeOperationResult.NotFound();
        }

        var requestedIds = request.Services.Select(service => service.ServiceId).Distinct().ToList();
        var activeServiceIds = await db.Services
            .Where(service => requestedIds.Contains(service.Id) && service.IsActive)
            .Select(service => service.Id)
            .ToListAsync(cancellationToken);

        var activePrestations = await db.ServicePrestations
            .Where(prestation => activeServiceIds.Contains(prestation.ServiceId) && prestation.IsActive)
            .Select(prestation => new { prestation.Id, prestation.ServiceId })
            .ToListAsync(cancellationToken);

        var activePrestationsByService = activePrestations
            .GroupBy(prestation => prestation.ServiceId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(prestation => prestation.Id).ToHashSet());

        var existingServiceCount = provider.Services.Count;
        provider.SyncCompanyServices(request.Services
            .Where(service => activeServiceIds.Contains(service.ServiceId))
            .Select(service => (
                service.ServiceId,
                ParseExperienceLevel(service.ExperienceLevel),
                Math.Max(0, service.YearsOfExperience),
                ParseProviderServicePriceTier(service.PriceTier))));

        foreach (var providerService in provider.Services.Where(service => service.IsActive))
        {
            var requestService = request.Services.LastOrDefault(service => service.ServiceId == providerService.ServiceId);
            if (requestService is null)
            {
                continue;
            }

            var allowedPrestationIds = activePrestationsByService.TryGetValue(providerService.ServiceId, out var ids)
                ? ids
                : new HashSet<Guid>();
            providerService.SyncPrestations(requestService.ServicePrestationIds
                .Where(allowedPrestationIds.Contains));
        }

        return CompanyEmployeeOperationResult.Ok(
            provider,
            new { ExistingServiceCount = existingServiceCount },
            new { RequestedServiceCount = request.Services.Count, AppliedServiceCount = activeServiceIds.Count });
    }

    public async Task<CompanyEmployeeOperationResult> SuspendAsync(Guid companyId, Guid employeeId, CancellationToken cancellationToken)
    {
        var provider = await db.Providers.FirstOrDefaultAsync(provider => provider.Id == employeeId && provider.CompanyId == companyId, cancellationToken);
        if (provider is null)
        {
            return CompanyEmployeeOperationResult.NotFound();
        }

        var before = new { provider.Status };
        provider.SuspendByCompany();
        return CompanyEmployeeOperationResult.Ok(provider, before, new { provider.Status });
    }

    public async Task<CompanyEmployeeOperationResult> UpdateAvailabilityAsync(
        Guid companyId,
        Guid employeeId,
        UpdateCompanyEmployeeAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        var provider = await db.Providers.FirstOrDefaultAsync(provider => provider.Id == employeeId && provider.CompanyId == companyId, cancellationToken);
        if (provider is null)
        {
            return CompanyEmployeeOperationResult.NotFound();
        }

        var before = new { provider.IsAvailable, provider.CurrentLatitude, provider.CurrentLongitude };
        provider.SetAvailability(request.IsAvailable, request.Latitude ?? provider.CurrentLatitude, request.Longitude ?? provider.CurrentLongitude);
        return CompanyEmployeeOperationResult.Ok(
            provider,
            before,
            new { provider.IsAvailable, provider.CurrentLatitude, provider.CurrentLongitude });
    }

    public async Task<CompanyEmployeeOperationResult> DeactivateAsync(Guid companyId, Guid employeeId, CancellationToken cancellationToken)
    {
        var provider = await db.Providers.FirstOrDefaultAsync(provider => provider.Id == employeeId && provider.CompanyId == companyId, cancellationToken);
        if (provider is null)
        {
            return CompanyEmployeeOperationResult.NotFound();
        }

        var before = new { provider.Status };
        provider.Deactivate();
        return CompanyEmployeeOperationResult.Ok(provider, before, new { provider.Status });
    }

    private static object SnapshotProfile(ProviderProfile provider)
    {
        return new
        {
            provider.FirstName,
            provider.LastName,
            provider.PhoneNumber,
            provider.Email,
            provider.DateOfBirth,
            provider.Address,
            provider.Gender,
            provider.EmploymentType,
            provider.YearsOfExperience,
            provider.MissionRadiusKm
        };
    }

    private static ExperienceLevel ParseExperienceLevel(string? value)
    {
        return Enum.TryParse<ExperienceLevel>(value, true, out var level)
            ? level
            : ExperienceLevel.Confirmed;
    }

    private static ProviderEmploymentType ParseProviderEmploymentType(string? value)
    {
        return Enum.TryParse<ProviderEmploymentType>(value, true, out var employmentType)
            ? employmentType
            : ProviderEmploymentType.CompanyEmployee;
    }

    private static ProviderGender ParseProviderGender(string? value)
    {
        return Enum.TryParse<ProviderGender>(value, true, out var gender)
            ? gender
            : ProviderGender.Unspecified;
    }

    private static ProviderServicePriceTier ParseProviderServicePriceTier(string? value)
    {
        return Enum.TryParse<ProviderServicePriceTier>(value, true, out var tier)
            ? tier
            : ProviderServicePriceTier.Normal;
    }
}
