using HomeService.Application.Abstractions;
using HomeService.Contracts.CompanyPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.CompanyPortal;

public sealed class CompanyInterimCandidateService(IAppDbContext db)
{
    public async Task<IReadOnlyList<CompanyInterimCandidateResponse>> ListAsync(Guid companyId, CancellationToken cancellationToken)
    {
        return await db.ProviderAffiliationRequests
            .AsNoTracking()
            .Include(request => request.Provider)
                .ThenInclude(provider => provider!.CandidateServices)
                    .ThenInclude(candidateService => candidateService.Service)
            .Where(request => request.CompanyId == companyId)
            .OrderBy(request => request.Status)
            .ThenByDescending(request => request.RequestedAt)
            .Select(request => new CompanyInterimCandidateResponse(
                request.Id,
                request.ProviderId,
                request.Provider!.FirstName,
                request.Provider.LastName,
                request.Provider.PhoneNumber,
                request.Provider.DateOfBirth,
                request.Provider.Address,
                request.Provider.Gender.ToString(),
                request.Provider.YearsOfExperience,
                request.Status.ToString(),
                request.Message,
                request.RequestedAt,
                request.Provider.CandidateServices
                    .Where(candidateService => candidateService.IsActive)
                    .OrderBy(candidateService => candidateService.Service!.Name)
                    .Select(candidateService => new CompanyInterimCandidateServiceResponse(
                        candidateService.ServiceId,
                        candidateService.Service!.Name,
                        candidateService.ExperienceLevel.ToString(),
                        candidateService.YearsOfExperience))
                    .ToList()))
            .ToListAsync(cancellationToken);
    }

    public async Task<CompanyInterimCandidateReviewResult> ApproveAsync(Guid companyId, Guid requestId, string? note, CancellationToken cancellationToken)
    {
        var request = await db.ProviderAffiliationRequests
            .Include(request => request.Provider)
                .ThenInclude(provider => provider!.CandidateServices)
            .FirstOrDefaultAsync(request => request.Id == requestId && request.CompanyId == companyId, cancellationToken);

        if (request?.Provider is null)
        {
            return CompanyInterimCandidateReviewResult.NotFound();
        }

        var provider = request.Provider;
        request.Approve(note);
        provider.AttachToCompanyAsTemporaryWorker(companyId);
        provider.SyncCompanyServices(provider.CandidateServices
            .Where(candidateService => candidateService.IsActive)
            .Select(candidateService => (
                candidateService.ServiceId,
                candidateService.ExperienceLevel,
                candidateService.YearsOfExperience,
                ProviderServicePriceTier.Normal)));

        await db.SaveChangesAsync(cancellationToken);
        return CompanyInterimCandidateReviewResult.Ok();
    }

    public async Task<CompanyInterimCandidateReviewResult> RejectAsync(Guid companyId, Guid requestId, string? note, CancellationToken cancellationToken)
    {
        var request = await db.ProviderAffiliationRequests
            .FirstOrDefaultAsync(request => request.Id == requestId && request.CompanyId == companyId, cancellationToken);

        if (request is null)
        {
            return CompanyInterimCandidateReviewResult.NotFound();
        }

        request.Reject(note);
        await db.SaveChangesAsync(cancellationToken);
        return CompanyInterimCandidateReviewResult.Ok();
    }
}

public sealed record CompanyInterimCandidateReviewResult(bool IsSuccess, bool IsNotFound)
{
    public static CompanyInterimCandidateReviewResult Ok() => new(true, false);
    public static CompanyInterimCandidateReviewResult NotFound() => new(false, true);
}
