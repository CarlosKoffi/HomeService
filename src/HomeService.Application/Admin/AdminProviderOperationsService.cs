using HomeService.Application.Abstractions;
using HomeService.Application.Auditing;
using HomeService.Application.Providers;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminProviderOperationsService(IAppDbContext db)
{
    public async Task<AdminProviderOperationResult> ApproveAsync(Guid providerId, CancellationToken cancellationToken)
        => await ApproveAsync(providerId, null, null, null, cancellationToken);

    public async Task<AdminProviderOperationResult> ApproveAsync(
        Guid providerId,
        string? note,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
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

        var missingDocuments = RequiredProviderDocumentsPolicy.GetMissingDocumentTypes(provider.Documents);
        if (missingDocuments.Count > 0)
        {
            return AdminProviderOperationResult.ValidationFailed(
                provider,
                $"Le dossier est incomplet. Pieces manquantes : {string.Join(", ", missingDocuments)}.");
        }

        var previousStatus = provider.Status;
        provider.Approve();
        AddAuditLog(
            actor,
            auditContext,
            "AdminProviderApproved",
            provider,
            previousStatus,
            string.IsNullOrWhiteSpace(note)
                ? "Prestataire valide par l'administration."
                : note.Trim());
        await db.SaveChangesAsync(cancellationToken);

        return AdminProviderOperationResult.Ok(provider, previousStatus);
    }

    public async Task<AdminProviderOperationResult> SuspendAsync(Guid providerId, CancellationToken cancellationToken)
        => await SuspendAsync(providerId, null, null, null, cancellationToken);

    public async Task<AdminProviderOperationResult> SuspendAsync(
        Guid providerId,
        string? note,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
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
        AddAuditLog(
            actor,
            auditContext,
            "AdminProviderSuspended",
            provider,
            previousStatus,
            string.IsNullOrWhiteSpace(note)
                ? "Prestataire suspendu par l'administration."
                : note.Trim());
        await db.SaveChangesAsync(cancellationToken);

        return AdminProviderOperationResult.Ok(provider, previousStatus);
    }

    public async Task<AdminProviderOperationResult> SetAvailabilityAsync(
        Guid providerId,
        bool isAvailable,
        string? note,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var provider = await db.Providers
            .FirstOrDefaultAsync(provider => provider.Id == providerId, cancellationToken);
        if (provider is null)
        {
            return AdminProviderOperationResult.NotFound();
        }

        if (provider.Status != ProviderStatus.Approved)
        {
            return AdminProviderOperationResult.ValidationFailed(
                provider,
                "Seul un prestataire valide peut etre force disponible.");
        }

        var previousStatus = provider.Status;
        var before = new
        {
            provider.Status,
            provider.IsAvailable,
            provider.CurrentLatitude,
            provider.CurrentLongitude
        };
        var latitude = provider.CurrentLatitude ?? provider.MissionLatitude ?? DefaultTestLatitude;
        var longitude = provider.CurrentLongitude ?? provider.MissionLongitude ?? DefaultTestLongitude;
        provider.SetAvailability(isAvailable, latitude, longitude);

        if (actor is not null)
        {
            var summary = string.IsNullOrWhiteSpace(note)
                ? isAvailable
                    ? "Prestataire force disponible depuis l'administration."
                    : "Prestataire force indisponible depuis l'administration."
                : note.Trim();
            db.AuditLogEntries.Add(AuditLogFactory.Create(
                actor,
                "AdminProviderAvailabilityForced",
                nameof(ProviderProfile),
                provider.Id,
                summary,
                auditContext,
                before,
                after: new
                {
                    provider.Status,
                    provider.IsAvailable,
                    provider.CurrentLatitude,
                    provider.CurrentLongitude
                }));
        }

        await db.SaveChangesAsync(cancellationToken);
        return AdminProviderOperationResult.Ok(provider, previousStatus);
    }

    private void AddAuditLog(
        AuditActor? actor,
        AuditRequestContext? auditContext,
        string action,
        ProviderProfile provider,
        ProviderStatus previousStatus,
        string summary)
    {
        if (actor is null)
        {
            return;
        }

        db.AuditLogEntries.Add(AuditLogFactory.Create(
            actor,
            action,
            nameof(ProviderProfile),
            provider.Id,
            summary,
            auditContext,
            before: new { Status = previousStatus.ToString() },
            after: new { provider.Status, provider.CompanyId }));
    }

    private const decimal DefaultTestLatitude = 5.3488m;
    private const decimal DefaultTestLongitude = -4.0031m;
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
