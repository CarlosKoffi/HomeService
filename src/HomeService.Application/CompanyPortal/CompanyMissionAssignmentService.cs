using HomeService.Application.Abstractions;
using HomeService.Contracts.CompanyPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.CompanyPortal;

public sealed class CompanyMissionAssignmentService(IAppDbContext db)
{
    private static readonly TimeSpan AssignmentAcceptanceWindow = TimeSpan.FromMinutes(3);

    public async Task<CompanyAssignableProvidersResult> ListAssignableProvidersAsync(Guid companyId, Guid missionId, CancellationToken cancellationToken)
    {
        var mission = await db.Missions
            .AsNoTracking()
            .FirstOrDefaultAsync(mission => mission.Id == missionId && mission.CompanyId == companyId, cancellationToken);
        if (mission is null)
        {
            return CompanyAssignableProvidersResult.NotFound();
        }

        var busyProviderIds = await db.ProviderMissionAssignments
            .AsNoTracking()
            .Where(assignment => assignment.CompanyId == companyId
                && (assignment.Status == ProviderMissionAssignmentStatus.Offered
                    || assignment.Status == ProviderMissionAssignmentStatus.Accepted
                    || assignment.Status == ProviderMissionAssignmentStatus.Started))
            .Select(assignment => assignment.ProviderId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var providers = await db.Providers
            .AsNoTracking()
            .Where(provider => provider.CompanyId == companyId
                && provider.Status == ProviderStatus.Approved
                && provider.Services.Any(service => service.IsActive && service.ServiceId == mission.ServiceId)
                && !busyProviderIds.Contains(provider.Id))
            .OrderByDescending(provider => provider.IsAvailable)
            .ThenBy(provider => provider.LastName)
            .Select(provider => new CompanyPortalAssignableProviderResponse(
                provider.Id,
                provider.FirstName + " " + provider.LastName,
                provider.PhoneNumber,
                provider.Status.ToString(),
                provider.IsAvailable,
                provider.EmploymentType.ToString(),
                provider.YearsOfExperience,
                provider.Services
                    .Where(service => service.IsActive && service.ServiceId == mission.ServiceId)
                    .Select(service => service.ExperienceLevel.ToString())
                    .FirstOrDefault() ?? "Confirmed",
                provider.Services
                    .Where(service => service.IsActive && service.ServiceId == mission.ServiceId)
                    .Select(service => service.PriceTier.ToString())
                    .FirstOrDefault() ?? "Normal",
                provider.Services
                    .Where(service => service.IsActive && service.ServiceId == mission.ServiceId)
                    .Select(service => service.Service!.NormalPriceAmount)
                    .FirstOrDefault(),
                provider.Services
                    .Where(service => service.IsActive && service.ServiceId == mission.ServiceId)
                    .Select(service => service.Service!.PremiumPriceAmount)
                    .FirstOrDefault(),
                provider.Services
                    .Where(service => service.IsActive && service.ServiceId == mission.ServiceId)
                    .Select(service => service.Service!.Currency)
                    .FirstOrDefault() ?? "XOF",
                provider.Documents.Any(document => document.DocumentType == ProviderDocumentType.Diploma),
                provider.Documents
                    .Where(document => document.DocumentType == ProviderDocumentType.Photo)
                    .OrderByDescending(document => document.CreatedAt)
                    .Select(document => $"/api/company-portal/provider-documents/{document.Id}/preview")
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return CompanyAssignableProvidersResult.Ok(providers);
    }

    public async Task<CompanyMissionAssignmentResult> AssignAsync(Guid companyId, Guid missionId, Guid providerId, CancellationToken cancellationToken)
    {
        var mission = await db.Missions.FirstOrDefaultAsync(mission => mission.Id == missionId && mission.CompanyId == companyId, cancellationToken);
        var provider = await db.Providers
            .Include(provider => provider.Services)
                .ThenInclude(service => service.Service)
            .FirstOrDefaultAsync(provider => provider.Id == providerId && provider.CompanyId == companyId, cancellationToken);

        var hasBlockingAssignment = await db.ProviderMissionAssignments.AnyAsync(assignment =>
            assignment.ProviderId == providerId
            && (assignment.Status == ProviderMissionAssignmentStatus.Offered
                || assignment.Status == ProviderMissionAssignmentStatus.Accepted
                || assignment.Status == ProviderMissionAssignmentStatus.Started),
            cancellationToken);

        var providerService = mission is null || provider is null
            ? null
            : provider.Services.FirstOrDefault(service => service.IsActive && service.ServiceId == mission.ServiceId);
        var policy = CompanyMissionAssignmentPolicy.Validate(
            mission is not null,
            provider is not null,
            provider?.Status == ProviderStatus.Approved,
            providerService is not null,
            hasBlockingAssignment);
        if (!policy.IsValid)
        {
            return policy.IsNotFound
                ? CompanyMissionAssignmentResult.NotFound(policy.Message ?? "Element introuvable.")
                : CompanyMissionAssignmentResult.Invalid(policy.Message ?? "Affectation impossible.");
        }

        var validMission = mission!;
        var validProvider = provider!;
        var validProviderService = providerService!;
        var priceAmount = validProviderService.PriceTier == ProviderServicePriceTier.Premium
            ? validProviderService.Service!.PremiumPriceAmount
            : validProviderService.Service!.NormalPriceAmount;
        validMission.Assign(providerId, companyId, priceAmount);

        var assignment = new ProviderMissionAssignment(
            validMission.Id,
            validProvider.Id,
            companyId,
            DateTimeOffset.UtcNow.Add(AssignmentAcceptanceWindow));
        db.ProviderMissionAssignments.Add(assignment);
        db.CompanyPortalActivities.Add(new CompanyPortalActivity(
            companyId,
            "mission",
            "Mission assignee",
            $"{validProvider.FullName} a recu une mission {validProviderService.Service!.Name}.",
            "blue",
            nameof(Mission),
            validMission.Id));

        await db.SaveChangesAsync(cancellationToken);

        return CompanyMissionAssignmentResult.Ok(new AssignCompanyMissionResponse(
            validMission.Id,
            validProvider.Id,
            assignment.Id,
            assignment.Status.ToString(),
            assignment.ExpiresAt));
    }
}

public sealed record CompanyAssignableProvidersResult(bool IsSuccess, IReadOnlyList<CompanyPortalAssignableProviderResponse> Providers, string? Message)
{
    public static CompanyAssignableProvidersResult Ok(IReadOnlyList<CompanyPortalAssignableProviderResponse> providers) => new(true, providers, null);
    public static CompanyAssignableProvidersResult NotFound() => new(false, [], "Mission introuvable.");
}

public sealed record CompanyMissionAssignmentResult(bool IsSuccess, AssignCompanyMissionResponse? Response, string? Message, bool IsNotFound)
{
    public static CompanyMissionAssignmentResult Ok(AssignCompanyMissionResponse response) => new(true, response, null, false);
    public static CompanyMissionAssignmentResult Invalid(string message) => new(false, null, message, false);
    public static CompanyMissionAssignmentResult NotFound(string message) => new(false, null, message, true);
}
