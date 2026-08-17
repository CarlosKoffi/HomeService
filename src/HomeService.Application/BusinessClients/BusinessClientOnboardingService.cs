using HomeService.Application.Abstractions;
using HomeService.Contracts.BusinessClients;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.BusinessClients;

public sealed class BusinessClientOnboardingService(IAppDbContext db)
{
    public static readonly BusinessClientDocumentType[] RequiredDocumentTypes =
    [
        BusinessClientDocumentType.BusinessRegistration,
        BusinessClientDocumentType.TaxCertificate,
        BusinessClientDocumentType.RepresentativeIdentity
    ];

    public async Task<BusinessClientProfileResponse?> GetAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var profile = await db.BusinessClientProfiles
            .AsNoTracking()
            .Include(item => item.Documents)
            .FirstOrDefaultAsync(item => item.CustomerProfileId == customerId, cancellationToken);
        return profile is null ? null : ToResponse(profile);
    }

    public async Task<BusinessClientProfileResponse> UpsertAsync(
        Guid customerId,
        UpsertBusinessClientProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerExists = await db.Customers.AnyAsync(item => item.Id == customerId, cancellationToken);
        if (!customerExists)
        {
            throw new InvalidOperationException("Le compte client est introuvable.");
        }

        var profile = await db.BusinessClientProfiles
            .Include(item => item.Documents)
            .FirstOrDefaultAsync(item => item.CustomerProfileId == customerId, cancellationToken);
        if (profile is null)
        {
            profile = new BusinessClientProfile(customerId);
            db.BusinessClientProfiles.Add(profile);
        }

        profile.Update(
            request.LegalName,
            request.TradeName,
            request.LegalForm,
            request.RegistrationNumber,
            request.TaxIdentificationNumber,
            request.Address,
            request.City,
            request.CountryCode,
            request.RepresentativeName,
            request.RepresentativeRole,
            request.ContactEmail,
            request.ContactPhone);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(profile);
    }

    public async Task<(BusinessClientDocumentResponse Document, string? ReplacedStoragePath)> AddDocumentAsync(
        Guid customerId,
        BusinessClientDocumentType documentType,
        string originalFileName,
        string storagePath,
        string contentType,
        long size,
        CancellationToken cancellationToken = default)
    {
        var profile = await db.BusinessClientProfiles
            .Include(item => item.Documents)
            .FirstOrDefaultAsync(item => item.CustomerProfileId == customerId, cancellationToken)
            ?? throw new InvalidOperationException("Enregistrez d'abord les informations de la societe.");
        if (!profile.CanEdit)
        {
            throw new InvalidOperationException("Les pieces ne peuvent plus etre modifiees pendant l'examen du dossier.");
        }

        string? replacedStoragePath = null;
        if (documentType != BusinessClientDocumentType.Other)
        {
            var previous = profile.Documents.FirstOrDefault(item => item.DocumentType == documentType);
            if (previous is not null)
            {
                replacedStoragePath = previous.StoragePath;
                db.BusinessClientDocuments.Remove(previous);
            }
        }

        var document = new BusinessClientDocument(
            profile.Id,
            documentType,
            originalFileName,
            storagePath,
            contentType,
            size);
        db.BusinessClientDocuments.Add(document);
        await db.SaveChangesAsync(cancellationToken);
        return (ToDocumentResponse(document), replacedStoragePath);
    }

    public async Task<string> RemoveDocumentAsync(
        Guid customerId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var profile = await db.BusinessClientProfiles
            .Include(item => item.Documents)
            .FirstOrDefaultAsync(item => item.CustomerProfileId == customerId, cancellationToken)
            ?? throw new KeyNotFoundException("Le dossier client entreprise est introuvable.");
        if (!profile.CanEdit)
        {
            throw new InvalidOperationException("Les pieces ne peuvent plus etre modifiees pendant l'examen du dossier.");
        }

        var document = profile.Documents.FirstOrDefault(item => item.Id == documentId)
            ?? throw new KeyNotFoundException("La piece est introuvable.");
        db.BusinessClientDocuments.Remove(document);
        await db.SaveChangesAsync(cancellationToken);
        return document.StoragePath;
    }

    public async Task<BusinessClientProfileResponse> SubmitAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var profile = await db.BusinessClientProfiles
            .Include(item => item.Documents)
            .FirstOrDefaultAsync(item => item.CustomerProfileId == customerId, cancellationToken)
            ?? throw new InvalidOperationException("Completez le dossier de la societe avant de le soumettre.");
        var missing = GetMissingRequiredDocuments(profile.Documents);
        if (missing.Count > 0)
        {
            throw new InvalidOperationException("Ajoutez toutes les pieces obligatoires avant de soumettre le dossier.");
        }

        profile.Submit();
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(profile);
    }

    public static BusinessClientProfileResponse ToResponse(BusinessClientProfile profile)
        => new(
            profile.Id,
            profile.CustomerProfileId,
            profile.LegalName,
            profile.TradeName,
            profile.LegalForm,
            profile.RegistrationNumber,
            profile.TaxIdentificationNumber,
            profile.Address,
            profile.City,
            profile.CountryCode,
            profile.RepresentativeName,
            profile.RepresentativeRole,
            profile.ContactEmail,
            profile.ContactPhone,
            profile.Status.ToString(),
            profile.SubmittedAt,
            profile.ReviewedAt,
            profile.ReviewNote,
            profile.CanEdit,
            profile.Documents
                .OrderBy(item => item.DocumentType)
                .ThenByDescending(item => item.CreatedAt)
                .Select(ToDocumentResponse)
                .ToArray(),
            GetMissingRequiredDocuments(profile.Documents));

    public static BusinessClientDocumentResponse ToDocumentResponse(BusinessClientDocument document)
        => new(
            document.Id,
            document.DocumentType.ToString(),
            document.OriginalFileName,
            document.ContentType,
            document.Size,
            document.ReviewStatus.ToString(),
            document.ReviewNote,
            document.CreatedAt);

    public static IReadOnlyList<string> GetMissingRequiredDocuments(IEnumerable<BusinessClientDocument> documents)
    {
        var presentTypes = documents
            .Where(item => item.ReviewStatus is DocumentReviewStatus.Pending or DocumentReviewStatus.Approved)
            .Select(item => item.DocumentType)
            .ToHashSet();
        return RequiredDocumentTypes
            .Where(type => !presentTypes.Contains(type))
            .Select(type => type.ToString())
            .ToArray();
    }

    public static bool HasAllRequiredDocuments(IEnumerable<BusinessClientDocument> documents)
        => GetMissingRequiredDocuments(documents).Count == 0;
}
