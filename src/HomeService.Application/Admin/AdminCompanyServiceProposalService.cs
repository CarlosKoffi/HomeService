using HomeService.Application.Abstractions;
using HomeService.Application.Companies;
using HomeService.Contracts.Services;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminCompanyServiceProposalService(IAppDbContext db)
{
    public async Task<CompanyServiceProposalListResponse> ListAsync(CancellationToken cancellationToken)
    {
        var catalog = await GetCatalogAsync(cancellationToken);
        var proposals = await db.CompanyApplicationServices
            .AsNoTracking()
            .Where(proposal =>
                proposal.MatchStatus == CompanyApplicationServiceMatchStatus.PendingMatch
                || proposal.MatchStatus == CompanyApplicationServiceMatchStatus.NeedsAdminReview
                || proposal.MatchedServiceId == null)
            .OrderByDescending(proposal => proposal.CreatedAt)
            .Select(proposal => new
            {
                proposal.Id,
                proposal.CompanyApplicationId,
                proposal.CompanyApplication!.CompanyId,
                CompanyName = proposal.CompanyApplication.CompanyName,
                proposal.RawName,
                proposal.NormalizedName,
                proposal.MatchStatus,
                proposal.MatchScore,
                proposal.MatchedServiceId,
                MatchedServiceName = proposal.MatchedService == null ? null : proposal.MatchedService.Name,
                proposal.MatchedServicePrestationId,
                MatchedServicePrestationName = proposal.MatchedServicePrestation == null ? null : proposal.MatchedServicePrestation.Name,
                proposal.CreatedAt
            })
            .Take(100)
            .ToListAsync(cancellationToken);

        return new CompanyServiceProposalListResponse(
            proposals
                .Select(proposal => new CompanyServiceProposalResponse(
                    proposal.Id,
                    proposal.CompanyApplicationId,
                    proposal.CompanyId,
                    proposal.CompanyName,
                    proposal.RawName,
                    proposal.NormalizedName,
                    proposal.MatchStatus.ToString(),
                    proposal.MatchScore,
                    proposal.MatchedServiceId,
                    proposal.MatchedServiceName,
                    proposal.MatchedServicePrestationId,
                    proposal.MatchedServicePrestationName,
                    proposal.CreatedAt,
                    CompanyApplicationServiceMatcher
                        .FindCandidates(proposal.RawName, catalog)
                        .Select(ToSuggestionResponse)
                        .ToList()))
                .ToList(),
            proposals.Count);
    }

    public async Task<CompanyServiceProposalActionResult> AttachAsync(
        Guid proposalId,
        AttachCompanyServiceProposalRequest request,
        CancellationToken cancellationToken)
    {
        var proposal = await db.CompanyApplicationServices.FirstOrDefaultAsync(item => item.Id == proposalId, cancellationToken);
        if (proposal is null)
        {
            return CompanyServiceProposalActionResult.NotFound("Service propose introuvable.");
        }

        if (request.ServicePrestationId.HasValue)
        {
            var prestation = await db.ServicePrestations
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == request.ServicePrestationId && item.IsActive, cancellationToken);
            if (prestation is null)
            {
                return CompanyServiceProposalActionResult.ValidationFailed("Prestation introuvable ou inactive.");
            }

            proposal.MarkAsMatchedPrestation(prestation.ServiceId, prestation.Id, 100);
            await db.SaveChangesAsync(cancellationToken);
            return CompanyServiceProposalActionResult.Ok("Service propose rattache a une prestation.");
        }

        if (!request.ServiceId.HasValue)
        {
            return CompanyServiceProposalActionResult.ValidationFailed("Selectionnez un service ou une prestation.");
        }

        var serviceExists = await db.Services.AnyAsync(
            service => service.Id == request.ServiceId.Value && service.IsActive,
            cancellationToken);
        if (!serviceExists)
        {
            return CompanyServiceProposalActionResult.ValidationFailed("Service introuvable ou inactif.");
        }

        proposal.MarkAsMatched(request.ServiceId.Value, 100);
        await db.SaveChangesAsync(cancellationToken);
        return CompanyServiceProposalActionResult.Ok("Service propose rattache au catalogue.");
    }

    public async Task<CompanyServiceProposalActionResult> CreatePrestationAsync(
        Guid proposalId,
        CreatePrestationFromCompanyServiceProposalRequest request,
        CancellationToken cancellationToken)
    {
        var proposal = await db.CompanyApplicationServices.FirstOrDefaultAsync(item => item.Id == proposalId, cancellationToken);
        if (proposal is null)
        {
            return CompanyServiceProposalActionResult.NotFound("Service propose introuvable.");
        }

        var service = await db.Services
            .Include(item => item.Prestations)
            .FirstOrDefaultAsync(item => item.Id == request.ServiceId && item.IsActive, cancellationToken);
        if (service is null)
        {
            return CompanyServiceProposalActionResult.ValidationFailed("Service parent introuvable ou inactif.");
        }

        var prestation = service.AddPrestation(
            string.IsNullOrWhiteSpace(request.Name) ? proposal.RawName : request.Name,
            request.Description,
            request.SortOrder,
            service.PriceMinAmount,
            service.PriceMaxAmount,
            service.Currency);
        proposal.MarkAsMatchedPrestation(service.Id, prestation.Id, 100);
        await db.SaveChangesAsync(cancellationToken);

        return CompanyServiceProposalActionResult.Ok("Prestation creee et service propose rattache.");
    }

    private async Task<IReadOnlyList<CompanyApplicationServiceCatalogItem>> GetCatalogAsync(CancellationToken cancellationToken)
    {
        var services = await db.Services
            .AsNoTracking()
            .Where(service => service.IsActive)
            .Select(service => new { service.Id, service.Name, service.NormalizedName })
            .ToListAsync(cancellationToken);
        var prestations = await db.ServicePrestations
            .AsNoTracking()
            .Where(prestation => prestation.IsActive && prestation.Service!.IsActive)
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

        return services
            .Select(service => new CompanyApplicationServiceCatalogItem(
                service.Id,
                service.Name,
                CompanyApplicationServiceMatcher.Normalize(service.NormalizedName),
                null,
                null,
                null))
            .Concat(prestations.Select(prestation => new CompanyApplicationServiceCatalogItem(
                prestation.ServiceId,
                prestation.ServiceName,
                CompanyApplicationServiceMatcher.Normalize(prestation.ServiceNormalizedName),
                prestation.Id,
                prestation.Name,
                CompanyApplicationServiceMatcher.Normalize(prestation.NormalizedName))))
            .ToList();
    }

    private static CompanyServiceProposalSuggestionResponse ToSuggestionResponse(CompanyApplicationServiceMatchCandidate candidate)
    {
        return new CompanyServiceProposalSuggestionResponse(
            candidate.ServiceId,
            candidate.ServiceName,
            candidate.ServicePrestationId,
            candidate.ServicePrestationName,
            candidate.Kind,
            candidate.Score);
    }
}

public sealed record CompanyServiceProposalActionResult(
    CompanyServiceProposalActionStatus Status,
    string Message)
{
    public bool IsSuccess => Status == CompanyServiceProposalActionStatus.Ok;

    public static CompanyServiceProposalActionResult Ok(string message)
        => new(CompanyServiceProposalActionStatus.Ok, message);

    public static CompanyServiceProposalActionResult NotFound(string message)
        => new(CompanyServiceProposalActionStatus.NotFound, message);

    public static CompanyServiceProposalActionResult ValidationFailed(string message)
        => new(CompanyServiceProposalActionStatus.ValidationFailed, message);
}

public enum CompanyServiceProposalActionStatus
{
    Ok,
    NotFound,
    ValidationFailed
}
