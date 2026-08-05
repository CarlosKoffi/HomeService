using HomeService.Application.Abstractions;
using HomeService.Contracts.ProviderPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.ProviderPortal;

public sealed class ProviderMobileProfileService(IAppDbContext db)
{
    public async Task<ProviderMobileProfileResult> GetAsync(Guid providerId, CancellationToken cancellationToken)
    {
        var provider = await db.Providers
            .AsNoTracking()
            .Include(item => item.Company)
            .Include(item => item.Documents)
            .Include(item => item.Services)
                .ThenInclude(item => item.Service)
            .Include(item => item.Services)
                .ThenInclude(item => item.Prestations)
                .ThenInclude(item => item.ServicePrestation)
            .FirstOrDefaultAsync(item => item.Id == providerId, cancellationToken);

        if (provider is null)
        {
            return ProviderMobileProfileResult.NotFound("Profil prestataire introuvable.");
        }

        var serviceIds = provider.Services
            .Where(item => item.IsActive)
            .Select(item => item.ServiceId)
            .Distinct()
            .ToList();

        var portfolioCountsByServiceId = serviceIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await db.ProviderServicePortfolioItems
                .AsNoTracking()
                .Where(item => item.ProviderId == provider.Id
                    && serviceIds.Contains(item.ServiceId)
                    && item.Status == PortfolioItemStatus.Approved)
                .GroupBy(item => item.ServiceId)
                .Select(group => new { ServiceId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.ServiceId, item => item.Count, cancellationToken);

        var documents = provider.Documents
            .OrderBy(item => item.DocumentType)
            .Select(item => new ProviderMobileProfileDocumentResponse(
                item.Id,
                item.DocumentType.ToString(),
                item.OriginalFileName,
                item.ContentType,
                $"/api/provider-portal/mobile/profile/documents/{item.Id}/preview"))
            .ToList();

        var profilePhotoUrl = provider.Documents
            .Where(item => item.DocumentType == ProviderDocumentType.Photo)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => $"/api/provider-portal/mobile/profile/documents/{item.Id}/preview")
            .FirstOrDefault();

        var canViewPrices = provider.EmploymentType == ProviderEmploymentType.TemporaryWorker;

        var portfolioItems = await db.ProviderServicePortfolioItems
            .AsNoTracking()
            .Include(item => item.Service)
            .Where(item => item.ProviderId == provider.Id)
            .OrderBy(item => item.DisplayOrder)
            .Select(item => new ProviderMobilePortfolioItemResponse(
                item.Id,
                item.ServiceId,
                item.Service != null ? item.Service.Name : "Service",
                item.OriginalFileName,
                item.ContentType,
                item.Status.ToString(),
                $"/api/provider-portal/mobile/profile/portfolio/{item.Id}/preview"))
            .ToListAsync(cancellationToken);

        var services = provider.Services
            .Where(item => item.IsActive && item.Service is not null)
            .OrderBy(item => item.Service!.Name)
            .Select(item =>
            {
                var service = item.Service!;
                portfolioCountsByServiceId.TryGetValue(service.Id, out var portfolioCount);
                var canReceiveMissions = provider.Status == ProviderStatus.Approved
                    && (!service.RequiresPortfolio || portfolioCount >= service.MinimumPortfolioItems);

                return new ProviderMobileProfileServiceResponse(
                    item.Id,
                    service.Id,
                    service.Name,
                    service.IconName,
                    item.ExperienceLevel.ToString(),
                    item.YearsOfExperience,
                    canViewPrices ? item.PriceTier.ToString() : null,
                    service.RequiresPortfolio,
                    service.MinimumPortfolioItems,
                    portfolioCount,
                    canReceiveMissions,
                    item.Prestations
                        .Where(prestation => prestation.IsActive && prestation.ServicePrestation is not null)
                        .OrderBy(prestation => prestation.ServicePrestation!.SortOrder)
                        .ThenBy(prestation => prestation.ServicePrestation!.Name)
                        .Select(prestation => new ProviderMobileProfilePrestationResponse(
                            prestation.ServicePrestationId,
                            prestation.ServicePrestation!.Name,
                            canViewPrices ? prestation.ServicePrestation.PriceMinAmount : null,
                            canViewPrices ? prestation.ServicePrestation.PriceMaxAmount : null,
                            canViewPrices ? prestation.ServicePrestation.Currency : null))
                        .ToList());
            })
            .ToList();

        var response = new ProviderMobileProfileResponse(
            provider.Id,
            provider.FirstName,
            provider.LastName,
            provider.FullName,
            provider.PhoneNumber,
            provider.Email,
            provider.Company?.Name ?? "En attente d'entreprise",
            provider.Status.ToString(),
            provider.EmploymentType.ToString(),
            provider.Status == ProviderStatus.Approved,
            provider.IsAvailable,
            provider.MissionRadiusKm,
            provider.Address,
            profilePhotoUrl,
            canViewPrices,
            BuildCompletion(provider),
            services,
            documents,
            portfolioItems);

        return ProviderMobileProfileResult.Ok(response);
    }

    private static ProviderMobileProfileCompletionResponse? BuildCompletion(ProviderProfile provider)
    {
        var missing = new List<string>();
        if (!provider.Documents.Any(document => document.DocumentType == ProviderDocumentType.Photo))
        {
            missing.Add("Photo de profil");
        }

        if (!provider.Documents.Any(document => document.DocumentType == ProviderDocumentType.IdentityDocument))
        {
            missing.Add("Piece d'identite");
        }

        if (!provider.Services.Any(service => service.IsActive))
        {
            missing.Add("Service actif");
        }

        if (provider.MissionLatitude is null || provider.MissionLongitude is null)
        {
            missing.Add("Zone de mission");
        }

        if (missing.Count == 0)
        {
            return null;
        }

        var percent = Math.Clamp(100 - missing.Count * 8, 0, 99);
        var message = missing.Count == 1
            ? $"Completez : {missing[0]}."
            : $"Completez {missing.Count} elements pour recevoir toutes les affectations.";

        return new ProviderMobileProfileCompletionResponse(percent, message, missing);
    }
}

public sealed record ProviderMobileProfileResult(
    bool IsSuccess,
    ProviderMobileProfileResponse? Response,
    string Message)
{
    public static ProviderMobileProfileResult Ok(ProviderMobileProfileResponse response)
    {
        return new ProviderMobileProfileResult(true, response, string.Empty);
    }

    public static ProviderMobileProfileResult NotFound(string message)
    {
        return new ProviderMobileProfileResult(false, null, message);
    }
}
