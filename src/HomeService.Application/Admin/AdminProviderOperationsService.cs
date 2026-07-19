using HomeService.Application.Abstractions;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminProviderOperationsService(IAppDbContext db)
{
    public async Task<AdminProviderOperationResult> ApproveAsync(Guid providerId, CancellationToken cancellationToken)
    {
        var provider = await db.Providers
            .Include(provider => provider.Services)
            .Include(provider => provider.Documents)
            .FirstOrDefaultAsync(provider => provider.Id == providerId, cancellationToken);
        if (provider is null)
        {
            return AdminProviderOperationResult.NotFound();
        }

        if (provider.CompanyId is null)
        {
            return AdminProviderOperationResult.ValidationFailed(provider, "Le prestataire doit etre rattache a une entreprise avant validation.");
        }

        if (provider.Status is ProviderStatus.Inactive or ProviderStatus.SuspendedByCompany)
        {
            return AdminProviderOperationResult.ValidationFailed(provider, "Ce prestataire est suspendu ou inactif.");
        }

        if (!provider.Services.Any(service => service.IsActive))
        {
            return AdminProviderOperationResult.ValidationFailed(provider, "Ajoutez au moins un service actif avant validation.");
        }

        if (!provider.Documents.Any(document => document.DocumentType == ProviderDocumentType.IdentityDocument))
        {
            return AdminProviderOperationResult.ValidationFailed(provider, "Ajoutez une piece d'identite avant validation.");
        }

        var previousStatus = provider.Status;
        provider.Approve();
        return AdminProviderOperationResult.Ok(provider, previousStatus);
    }

    public async Task<AdminProviderOperationResult> SuspendAsync(Guid providerId, CancellationToken cancellationToken)
    {
        var provider = await db.Providers
            .FirstOrDefaultAsync(provider => provider.Id == providerId, cancellationToken);
        if (provider is null)
        {
            return AdminProviderOperationResult.NotFound();
        }

        if (provider.Status == ProviderStatus.SuspendedByPlatform)
        {
            return AdminProviderOperationResult.ValidationFailed(provider, "Ce prestataire est deja suspendu par la plateforme.");
        }

        if (provider.Status == ProviderStatus.Inactive)
        {
            return AdminProviderOperationResult.ValidationFailed(provider, "Ce prestataire est inactif.");
        }

        var previousStatus = provider.Status;
        provider.SuspendByPlatform();
        return AdminProviderOperationResult.Ok(provider, previousStatus);
    }
}

public sealed record AdminProviderOperationResult(
    AdminProviderOperationStatus Status,
    ProviderProfile? Provider,
    ProviderStatus? PreviousStatus,
    string? Message)
{
    public static AdminProviderOperationResult Ok(ProviderProfile provider, ProviderStatus previousStatus)
        => new(AdminProviderOperationStatus.Ok, provider, previousStatus, null);

    public static AdminProviderOperationResult NotFound()
        => new(AdminProviderOperationStatus.NotFound, null, null, "Prestataire introuvable.");

    public static AdminProviderOperationResult ValidationFailed(ProviderProfile provider, string message)
        => new(AdminProviderOperationStatus.ValidationFailed, provider, provider.Status, message);
}

public enum AdminProviderOperationStatus
{
    Ok = 0,
    NotFound = 1,
    ValidationFailed = 2
}
