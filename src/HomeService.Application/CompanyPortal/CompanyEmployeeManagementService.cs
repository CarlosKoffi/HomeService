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
            .FirstOrDefaultAsync(provider => provider.Id == employeeId && provider.CompanyId == companyId, cancellationToken);
        if (provider is null)
        {
            return CompanyEmployeeOperationResult.NotFound();
        }

        var providerServices = await db.ProviderServices
            .Include(service => service.Prestations)
            .Where(service => service.ProviderId == employeeId && service.CompanyId == companyId)
            .ToListAsync(cancellationToken);

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

        var existingServiceCount = providerServices.Count(service => service.IsActive);

        var requestedServices = request.Services
            .Where(service => activeServiceIds.Contains(service.ServiceId))
            .GroupBy(service => service.ServiceId)
            .Select(group => group.Last())
            .ToList();

        var requestedServiceIds = requestedServices.Select(service => service.ServiceId).ToHashSet();
        foreach (var existingService in providerServices.Where(service => service.IsActive && !requestedServiceIds.Contains(service.ServiceId)))
        {
            existingService.Deactivate();
        }

        foreach (var requestedService in requestedServices)
        {
            var providerService = providerServices.FirstOrDefault(service => service.ServiceId == requestedService.ServiceId);
            if (providerService is null)
            {
                providerService = new ProviderService(
                    employeeId,
                    companyId,
                    requestedService.ServiceId,
                    ParseExperienceLevel(requestedService.ExperienceLevel),
                    Math.Max(0, requestedService.YearsOfExperience),
                    ParseProviderServicePriceTier(requestedService.PriceTier));
                db.ProviderServices.Add(providerService);
                providerServices.Add(providerService);
            }
            else
            {
                providerService.UpdateCompanyExperience(
                    ParseExperienceLevel(requestedService.ExperienceLevel),
                    Math.Max(0, requestedService.YearsOfExperience),
                    ParseProviderServicePriceTier(requestedService.PriceTier));
            }

            var allowedPrestationIds = activePrestationsByService.TryGetValue(providerService.ServiceId, out var ids)
                ? ids
                : new HashSet<Guid>();
            providerService.SyncPrestations(requestedService.ServicePrestationIds.Where(allowedPrestationIds.Contains));
        }

        return CompanyEmployeeOperationResult.Ok(
            provider,
            new { ExistingServiceCount = existingServiceCount },
            new { RequestedServiceCount = request.Services.Count, AppliedServiceCount = activeServiceIds.Count });
    }

    public async Task<CompanyEmployeeDocumentOperationResult> ReplaceDocumentAsync(
        Guid companyId,
        Guid employeeId,
        ProviderDocumentType documentType,
        string originalFileName,
        string storagePath,
        string contentType,
        CancellationToken cancellationToken)
    {
        var provider = await db.Providers
            .FirstOrDefaultAsync(provider => provider.Id == employeeId && provider.CompanyId == companyId, cancellationToken);
        if (provider is null)
        {
            return CompanyEmployeeDocumentOperationResult.NotFound();
        }

        var existingDocumentCount = await db.ProviderDocuments
            .Where(document => document.ProviderId == employeeId && document.DocumentType == documentType)
            .CountAsync(cancellationToken);

        var document = new ProviderDocument(employeeId, documentType, originalFileName, storagePath, contentType);
        db.ProviderDocuments.Add(document);

        return CompanyEmployeeDocumentOperationResult.Ok(
            provider,
            document,
            [],
            new { ExistingDocumentCount = existingDocumentCount, DocumentType = documentType },
            new { DocumentType = documentType, OriginalFileName = originalFileName, ContentType = contentType });
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

    public async Task<CompanyEmployeeOperationResult> ApproveAsync(Guid companyId, Guid employeeId, CancellationToken cancellationToken)
    {
        var provider = await db.Providers
            .Include(provider => provider.Services)
            .Include(provider => provider.Documents)
            .FirstOrDefaultAsync(provider => provider.Id == employeeId && provider.CompanyId == companyId, cancellationToken);
        if (provider is null)
        {
            return CompanyEmployeeOperationResult.NotFound();
        }

        if (provider.Status is ProviderStatus.Inactive or ProviderStatus.SuspendedByCompany or ProviderStatus.SuspendedByPlatform)
        {
            return CompanyEmployeeOperationResult.ValidationFailed(provider, "Ce prestataire est suspendu ou inactif. Reouvrez son dossier avant validation.");
        }

        if (!provider.Services.Any(service => service.IsActive))
        {
            return CompanyEmployeeOperationResult.ValidationFailed(provider, "Ajoutez au moins un service actif avant de valider ce prestataire.");
        }

        if (!provider.Documents.Any(document => document.DocumentType == ProviderDocumentType.IdentityDocument))
        {
            return CompanyEmployeeOperationResult.ValidationFailed(provider, "Ajoutez une piece d'identite avant de valider ce prestataire.");
        }

        var before = new { provider.Status, provider.IsAvailable };
        provider.Approve();
        return CompanyEmployeeOperationResult.Ok(provider, before, new { provider.Status, provider.IsAvailable });
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
