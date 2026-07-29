using HomeService.Application.Abstractions;
using HomeService.Contracts.ProviderPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.ProviderPortal;

public sealed class ProviderMobileProfileUpdateService(IAppDbContext db)
{
    public async Task<ProviderMobileDocumentUploadResult> AddDocumentAsync(
        Guid providerId,
        ProviderDocumentType documentType,
        string originalFileName,
        string storagePath,
        string contentType,
        CancellationToken cancellationToken)
    {
        var providerExists = await db.Providers
            .AnyAsync(provider => provider.Id == providerId, cancellationToken);
        if (!providerExists)
        {
            return ProviderMobileDocumentUploadResult.NotFound("Profil prestataire introuvable.");
        }

        var existingDocumentCount = await db.ProviderDocuments
            .Where(document => document.ProviderId == providerId && document.DocumentType == documentType)
            .CountAsync(cancellationToken);

        var document = new ProviderDocument(providerId, documentType, originalFileName, storagePath, contentType);
        db.ProviderDocuments.Add(document);

        return ProviderMobileDocumentUploadResult.Ok(
            new ProviderMobileProfileDocumentResponse(
                document.Id,
                document.DocumentType.ToString(),
                document.OriginalFileName,
                document.ContentType,
                $"/api/provider-portal/mobile/profile/documents/{document.Id}/preview"),
            new { ExistingDocumentCount = existingDocumentCount, DocumentType = documentType },
            new { DocumentType = documentType, OriginalFileName = originalFileName, ContentType = contentType });
    }

    public async Task<ProviderMobilePortfolioUploadResult> AddPortfolioItemAsync(
        Guid providerId,
        Guid serviceId,
        string originalFileName,
        string storagePath,
        string contentType,
        CancellationToken cancellationToken)
    {
        var hasProviderService = await db.ProviderServices
            .AnyAsync(providerService =>
                providerService.ProviderId == providerId
                && providerService.ServiceId == serviceId
                && providerService.IsActive,
                cancellationToken);
        if (!hasProviderService)
        {
            return ProviderMobilePortfolioUploadResult.NotFound("Service prestataire introuvable ou inactif.");
        }

        var nextOrder = await db.ProviderServicePortfolioItems
            .Where(item => item.ProviderId == providerId && item.ServiceId == serviceId)
            .Select(item => (int?)item.DisplayOrder)
            .MaxAsync(cancellationToken) ?? 0;
        nextOrder++;

        var item = new ProviderServicePortfolioItem(
            providerId,
            serviceId,
            originalFileName,
            storagePath,
            contentType,
            nextOrder);
        db.ProviderServicePortfolioItems.Add(item);

        return ProviderMobilePortfolioUploadResult.Ok(
            new ProviderMobilePortfolioUploadResponse(
                item.Id,
                serviceId,
                item.OriginalFileName,
                item.ContentType,
                item.Status.ToString(),
                $"/api/provider-portal/mobile/profile/portfolio/{item.Id}/preview"),
            new { ServiceId = serviceId, NextDisplayOrder = nextOrder });
    }
}

public sealed record ProviderMobileDocumentUploadResult(
    bool IsSuccess,
    ProviderMobileProfileDocumentResponse? Response,
    string Message,
    object? Before,
    object? After)
{
    public static ProviderMobileDocumentUploadResult Ok(
        ProviderMobileProfileDocumentResponse response,
        object? before,
        object? after)
    {
        return new ProviderMobileDocumentUploadResult(true, response, string.Empty, before, after);
    }

    public static ProviderMobileDocumentUploadResult NotFound(string message)
    {
        return new ProviderMobileDocumentUploadResult(false, null, message, null, null);
    }
}

public sealed record ProviderMobilePortfolioUploadResult(
    bool IsSuccess,
    ProviderMobilePortfolioUploadResponse? Response,
    string Message,
    object? After)
{
    public static ProviderMobilePortfolioUploadResult Ok(
        ProviderMobilePortfolioUploadResponse response,
        object? after)
    {
        return new ProviderMobilePortfolioUploadResult(true, response, string.Empty, after);
    }

    public static ProviderMobilePortfolioUploadResult NotFound(string message)
    {
        return new ProviderMobilePortfolioUploadResult(false, null, message, null);
    }
}
