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
            .AsNoTracking()
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

        var existingServices = await db.ProviderServices
            .AsNoTracking()
            .Where(providerService => providerService.ProviderId == employeeId)
            .ToListAsync(cancellationToken);
        var existingServiceCount = existingServices.Count;

        var requestedServices = request.Services
            .Where(service => activeServiceIds.Contains(service.ServiceId))
            .GroupBy(service => service.ServiceId)
            .Select(group => group.Last())
            .ToList();
        var requestedActiveIds = requestedServices.Select(service => service.ServiceId).ToHashSet();

        await db.ProviderServices
            .Where(service => service.ProviderId == employeeId && service.IsActive && !requestedActiveIds.Contains(service.ServiceId))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(service => service.IsActive, false)
                .SetProperty(service => service.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken);

        foreach (var requestedService in requestedServices)
        {
            var providerService = existingServices.FirstOrDefault(service => service.ServiceId == requestedService.ServiceId);
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
                existingServices.Add(providerService);
            }
            else
            {
                await db.ProviderServices
                    .Where(service => service.Id == providerService.Id)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(service => service.ExperienceLevel, ParseExperienceLevel(requestedService.ExperienceLevel))
                        .SetProperty(service => service.YearsOfExperience, Math.Max(0, requestedService.YearsOfExperience))
                        .SetProperty(service => service.PriceTier, ParseProviderServicePriceTier(requestedService.PriceTier))
                        .SetProperty(service => service.CompanyValidatedAt, DateTimeOffset.UtcNow)
                        .SetProperty(service => service.IsActive, true)
                        .SetProperty(service => service.UpdatedAt, DateTimeOffset.UtcNow),
                        cancellationToken);
            }

            var allowedPrestationIds = activePrestationsByService.TryGetValue(providerService.ServiceId, out var ids)
                ? ids
                : new HashSet<Guid>();
            await SyncServicePrestationsAsync(providerService.Id, requestedService.ServicePrestationIds.Where(allowedPrestationIds.Contains), cancellationToken);
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
            .AsNoTracking()
            .FirstOrDefaultAsync(provider => provider.Id == employeeId && provider.CompanyId == companyId, cancellationToken);
        if (provider is null)
        {
            return CompanyEmployeeDocumentOperationResult.NotFound();
        }

        var existingDocuments = await db.ProviderDocuments
            .Where(document => document.ProviderId == employeeId && document.DocumentType == documentType)
            .OrderByDescending(document => document.CreatedAt)
            .ToListAsync(cancellationToken);

        var document = existingDocuments.FirstOrDefault();
        var replacedPaths = existingDocuments.Select(existing => existing.StoragePath).ToList();
        if (document is null)
        {
            document = new ProviderDocument(employeeId, documentType, originalFileName, storagePath, contentType);
            db.ProviderDocuments.Add(document);
        }
        else
        {
            document.ReplaceFile(originalFileName, storagePath, contentType);
        }

        foreach (var duplicate in existingDocuments.Skip(1))
        {
            db.ProviderDocuments.Remove(duplicate);
        }

        return CompanyEmployeeDocumentOperationResult.Ok(
            provider,
            document,
            replacedPaths,
            new { ReplacedDocumentCount = replacedPaths.Count, DocumentType = documentType },
            new { DocumentType = documentType, OriginalFileName = originalFileName, ContentType = contentType });
    }

    private async Task SyncServicePrestationsAsync(
        Guid providerServiceId,
        IEnumerable<Guid> servicePrestationIds,
        CancellationToken cancellationToken)
    {
        var requestedIds = servicePrestationIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToHashSet();

        await db.ProviderServicePrestations
            .Where(prestation => prestation.ProviderServiceId == providerServiceId
                && prestation.IsActive
                && !requestedIds.Contains(prestation.ServicePrestationId))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(prestation => prestation.IsActive, false)
                .SetProperty(prestation => prestation.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken);

        if (requestedIds.Count == 0)
        {
            return;
        }

        await db.ProviderServicePrestations
            .Where(prestation => prestation.ProviderServiceId == providerServiceId
                && requestedIds.Contains(prestation.ServicePrestationId)
                && !prestation.IsActive)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(prestation => prestation.IsActive, true)
                .SetProperty(prestation => prestation.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken);

        var existingIds = await db.ProviderServicePrestations
            .AsNoTracking()
            .Where(prestation => prestation.ProviderServiceId == providerServiceId
                && requestedIds.Contains(prestation.ServicePrestationId))
            .Select(prestation => prestation.ServicePrestationId)
            .ToListAsync(cancellationToken);

        foreach (var missingId in requestedIds.Except(existingIds))
        {
            db.ProviderServicePrestations.Add(new ProviderServicePrestation(providerServiceId, missingId));
        }
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
