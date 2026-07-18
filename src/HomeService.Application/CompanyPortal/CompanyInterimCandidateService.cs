using HomeService.Application.Abstractions;
using HomeService.Contracts.CompanyPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.CompanyPortal;

public sealed class CompanyInterimCandidateService(IAppDbContext db)
{
    public async Task<CompanyInterimSettingsResult> GetSettingsAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var company = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(company => company.Id == companyId && company.Status != CompanyStatus.Suspended, cancellationToken);
        if (company is null)
        {
            return CompanyInterimSettingsResult.NotFound();
        }

        return CompanyInterimSettingsResult.Ok(ToSettingsResponse(company));
    }

    public async Task<CompanyInterimSettingsResult> UpdateSettingsAsync(
        Guid companyId,
        UpdateCompanyInterimSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var company = await db.Companies
            .FirstOrDefaultAsync(company => company.Id == companyId && company.Status != CompanyStatus.Suspended, cancellationToken);
        if (company is null)
        {
            return CompanyInterimSettingsResult.NotFound();
        }

        company.SetInterimApplications(request.AcceptsInterimApplications);
        await db.SaveChangesAsync(cancellationToken);

        return CompanyInterimSettingsResult.Ok(ToSettingsResponse(company));
    }

    public async Task<IReadOnlyList<CompanyInterimCandidateResponse>> ListAsync(Guid companyId, CancellationToken cancellationToken)
    {
        return await db.ProviderAffiliationRequests
            .AsNoTracking()
            .Include(request => request.Provider)
                .ThenInclude(provider => provider!.CandidateServices)
                    .ThenInclude(candidateService => candidateService.Service)
            .Include(request => request.Company)
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
                request.Company!.Name,
                request.ReviewNote,
                request.RequestedAt,
                request.ReviewedAt,
                request.Provider.CandidateServices
                    .Where(candidateService => candidateService.IsActive)
                    .OrderBy(candidateService => candidateService.Service!.Name)
                    .Select(candidateService => new CompanyInterimCandidateServiceResponse(
                        candidateService.ServiceId,
                        candidateService.Service!.Name,
                        candidateService.ExperienceLevel.ToString(),
                        candidateService.YearsOfExperience))
                    .ToList(),
                db.ProviderAffiliationRequests
                    .Where(otherRequest => otherRequest.ProviderId == request.ProviderId)
                    .OrderByDescending(otherRequest => otherRequest.RequestedAt)
                    .Select(otherRequest => new CompanyInterimCandidateAffiliationResponse(
                        otherRequest.Id,
                        otherRequest.CompanyId,
                        otherRequest.Company!.Name,
                        otherRequest.Status.ToString(),
                        otherRequest.RequestedAt,
                        otherRequest.ReviewedAt,
                        otherRequest.CompanyId == companyId))
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

        var otherPendingRequests = await db.ProviderAffiliationRequests
            .Where(otherRequest =>
                otherRequest.ProviderId == provider.Id
                && otherRequest.Id != request.Id
                && otherRequest.Status == ProviderAffiliationRequestStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var otherRequest in otherPendingRequests)
        {
            otherRequest.Cancel("Candidature cloturee automatiquement apres validation par une autre entreprise.");
        }

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

    private static CompanyInterimSettingsResponse ToSettingsResponse(Company company)
    {
        var message = company.AcceptsInterimApplications
            ? "Votre entreprise peut recevoir des candidatures interimaires compatibles avec vos services."
            : "Votre entreprise ne recoit pas de nouvelles candidatures interimaires pour le moment.";

        return new CompanyInterimSettingsResponse(company.Id, company.AcceptsInterimApplications, message);
    }
}

public sealed record CompanyInterimCandidateReviewResult(bool IsSuccess, bool IsNotFound)
{
    public static CompanyInterimCandidateReviewResult Ok() => new(true, false);
    public static CompanyInterimCandidateReviewResult NotFound() => new(false, true);
}

public sealed record CompanyInterimSettingsResult(bool IsSuccess, bool IsNotFound, CompanyInterimSettingsResponse? Response, string? Message)
{
    public static CompanyInterimSettingsResult Ok(CompanyInterimSettingsResponse response) => new(true, false, response, null);
    public static CompanyInterimSettingsResult NotFound() => new(false, true, null, "Entreprise introuvable ou inactive.");
}
