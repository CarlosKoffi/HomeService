using HomeService.Application.Abstractions;
using HomeService.Application.BusinessClients;
using HomeService.Contracts.BusinessClients;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminBusinessClientReviewService(IAppDbContext db)
{
    public async Task<IReadOnlyList<AdminBusinessClientListItemResponse>> ListAsync(
        BusinessClientStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = db.BusinessClientProfiles
            .AsNoTracking()
            .Include(item => item.CustomerProfile)
            .Include(item => item.Documents)
            .AsQueryable();
        if (status is not null)
        {
            query = query.Where(item => item.Status == status.Value);
        }

        return await query
            .OrderByDescending(item => item.SubmittedAt ?? item.CreatedAt)
            .Select(item => new AdminBusinessClientListItemResponse(
                item.Id,
                item.CustomerProfileId,
                item.LegalName,
                item.TradeName,
                item.CustomerProfile == null
                    ? item.RepresentativeName
                    : item.CustomerProfile.FirstName + " " + item.CustomerProfile.LastName,
                item.ContactEmail,
                item.ContactPhone,
                item.Status.ToString(),
                item.Documents.Count,
                item.SubmittedAt,
                item.UpdatedAt ?? item.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminBusinessClientDetailResponse?> GetAsync(
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        var profile = await db.BusinessClientProfiles
            .AsNoTracking()
            .Include(item => item.CustomerProfile)
            .Include(item => item.Documents)
            .FirstOrDefaultAsync(item => item.Id == profileId, cancellationToken);
        if (profile is null)
        {
            return null;
        }

        var customerName = profile.CustomerProfile is null
            ? profile.RepresentativeName
            : $"{profile.CustomerProfile.FirstName} {profile.CustomerProfile.LastName}";
        return new AdminBusinessClientDetailResponse(
            BusinessClientOnboardingService.ToResponse(profile),
            customerName,
            profile.CustomerProfile?.PhoneNumber ?? profile.ContactPhone,
            profile.CustomerProfile?.Email);
    }

    public Task<BusinessClientProfileResponse> MarkUnderReviewAsync(Guid profileId, CancellationToken cancellationToken = default)
        => MutateAsync(profileId, profile => profile.MarkUnderReview(), true, cancellationToken);

    public Task<BusinessClientProfileResponse> RequestMoreInformationAsync(Guid profileId, string? note, CancellationToken cancellationToken = default)
        => MutateAsync(profileId, profile => profile.RequestMoreInformation(RequireNote(note)), false, cancellationToken);

    public Task<BusinessClientProfileResponse> ApproveAsync(Guid profileId, string? note, CancellationToken cancellationToken = default)
        => MutateAsync(profileId, profile => profile.Approve(note), true, cancellationToken);

    public Task<BusinessClientProfileResponse> RejectAsync(Guid profileId, string? note, CancellationToken cancellationToken = default)
        => MutateAsync(profileId, profile => profile.Reject(RequireNote(note)), false, cancellationToken);

    public async Task<(string StoragePath, string ContentType, string FileName)?> GetDocumentAsync(
        Guid profileId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var document = await db.BusinessClientDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == documentId && item.BusinessClientProfileId == profileId,
                cancellationToken);
        return document is null ? null : (document.StoragePath, document.ContentType, document.OriginalFileName);
    }

    private async Task<BusinessClientProfileResponse> MutateAsync(
        Guid profileId,
        Action<BusinessClientProfile> mutation,
        bool requireCompleteDossier,
        CancellationToken cancellationToken)
    {
        var profile = await db.BusinessClientProfiles
            .Include(item => item.Documents)
            .FirstOrDefaultAsync(item => item.Id == profileId, cancellationToken)
            ?? throw new KeyNotFoundException("Le dossier client entreprise est introuvable.");
        if (requireCompleteDossier && !BusinessClientOnboardingService.HasAllRequiredDocuments(profile.Documents))
        {
            throw new InvalidOperationException("Le dossier ne peut pas etre examine ou valide avant le depot de 100 % des justificatifs obligatoires.");
        }
        mutation(profile);
        await db.SaveChangesAsync(cancellationToken);
        return BusinessClientOnboardingService.ToResponse(profile);
    }

    private static string RequireNote(string? note)
        => string.IsNullOrWhiteSpace(note)
            ? throw new InvalidOperationException("Une note est obligatoire pour cette decision.")
            : note.Trim();
}
