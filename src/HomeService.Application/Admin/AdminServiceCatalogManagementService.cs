using HomeService.Application.Abstractions;
using HomeService.Application.Auditing;
using HomeService.Contracts.Services;
using HomeService.Domain.Common;
using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminServiceCatalogManagementService(IAppDbContext db)
{
    public async Task<AdminServiceCatalogOperationResult<ServiceSummaryResponse>> CreateServiceAsync(
        UpsertServiceRequest request,
        CancellationToken cancellationToken)
        => await CreateServiceAsync(request, null, null, cancellationToken);

    public async Task<AdminServiceCatalogOperationResult<ServiceSummaryResponse>> CreateServiceAsync(
        UpsertServiceRequest request,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var validationMessage = ValidateServiceRequest(request);
        if (validationMessage is not null)
        {
            return AdminServiceCatalogOperationResult<ServiceSummaryResponse>.ValidationFailed(validationMessage);
        }

        var normalizedName = CatalogNameNormalizer.Normalize(request.Name);
        var existing = await db.Services
            .AsNoTracking()
            .AnyAsync(service => service.NormalizedName == normalizedName, cancellationToken);

        if (existing)
        {
            return AdminServiceCatalogOperationResult<ServiceSummaryResponse>.Conflict("Un service avec ce nom existe deja.");
        }

        var service = new Service(request.Name, request.Description, createdByCompanyId: null);
        service.UpdateDetails(request.Name, request.Description, request.IconName);
        service.UpdateMedia(request.IconUrl, request.ImageUrl);
        service.UpdatePriceRange(GetPriceMin(request), GetPriceMax(request), request.Currency);
        service.Approve();

        db.Services.Add(service);
        var response = ToServiceResponse(service);
        AddAuditLog(
            actor,
            auditContext,
            "AdminServiceCreated",
            nameof(Service),
            response.Id,
            $"Service '{service.Name}' cree dans le catalogue.",
            before: null,
            after: response);
        await db.SaveChangesAsync(cancellationToken);

        return AdminServiceCatalogOperationResult<ServiceSummaryResponse>.Ok(
            $"Service '{service.Name}' cree dans le catalogue.",
            response,
            before: null,
            after: response);
    }

    public async Task<AdminServiceCatalogOperationResult<ServiceSummaryResponse>> UpdateServiceAsync(
        Guid serviceId,
        UpsertServiceRequest request,
        CancellationToken cancellationToken)
        => await UpdateServiceAsync(serviceId, request, null, null, cancellationToken);

    public async Task<AdminServiceCatalogOperationResult<ServiceSummaryResponse>> UpdateServiceAsync(
        Guid serviceId,
        UpsertServiceRequest request,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var validationMessage = ValidateServiceRequest(request);
        if (validationMessage is not null)
        {
            return AdminServiceCatalogOperationResult<ServiceSummaryResponse>.ValidationFailed(validationMessage);
        }

        var normalizedName = CatalogNameNormalizer.Normalize(request.Name);
        var duplicate = await db.Services.AnyAsync(
            service => service.Id != serviceId && service.NormalizedName == normalizedName,
            cancellationToken);
        if (duplicate)
        {
            return AdminServiceCatalogOperationResult<ServiceSummaryResponse>.Conflict("Un autre service utilise deja ce nom.");
        }

        var service = await db.Services
            .Include(item => item.Prestations)
            .FirstOrDefaultAsync(item => item.Id == serviceId, cancellationToken);
        if (service is null)
        {
            return AdminServiceCatalogOperationResult<ServiceSummaryResponse>.NotFound("Service introuvable.");
        }

        var before = ToServiceResponse(service);
        service.UpdateDetails(request.Name, request.Description, request.IconName);
        service.UpdateMedia(request.IconUrl, request.ImageUrl);
        service.UpdatePriceRange(GetPriceMin(request), GetPriceMax(request), request.Currency);

        var after = ToServiceResponse(service);
        AddAuditLog(
            actor,
            auditContext,
            "AdminServiceUpdated",
            nameof(Service),
            after.Id,
            "Service modifie dans le catalogue.",
            before,
            after);
        await db.SaveChangesAsync(cancellationToken);

        return AdminServiceCatalogOperationResult<ServiceSummaryResponse>.Ok(
            "Service modifie dans le catalogue.",
            after,
            before,
            after);
    }

    public Task<AdminServiceCatalogOperationResult<ServiceSummaryResponse>> ActivateServiceAsync(
        Guid serviceId,
        CancellationToken cancellationToken)
    {
        return ActivateServiceAsync(serviceId, null, null, cancellationToken);
    }

    public Task<AdminServiceCatalogOperationResult<ServiceSummaryResponse>> ActivateServiceAsync(
        Guid serviceId,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        return SetServiceActiveStateAsync(serviceId, isActive: true, actor, auditContext, cancellationToken);
    }

    public Task<AdminServiceCatalogOperationResult<ServiceSummaryResponse>> DeactivateServiceAsync(
        Guid serviceId,
        CancellationToken cancellationToken)
    {
        return DeactivateServiceAsync(serviceId, null, null, cancellationToken);
    }

    public Task<AdminServiceCatalogOperationResult<ServiceSummaryResponse>> DeactivateServiceAsync(
        Guid serviceId,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        return SetServiceActiveStateAsync(serviceId, isActive: false, actor, auditContext, cancellationToken);
    }

    public async Task<AdminServiceCatalogOperationResult<ServicePrestationSummaryResponse>> CreatePrestationAsync(
        Guid serviceId,
        UpsertServicePrestationRequest request,
        CancellationToken cancellationToken)
        => await CreatePrestationAsync(serviceId, request, null, null, cancellationToken);

    public async Task<AdminServiceCatalogOperationResult<ServicePrestationSummaryResponse>> CreatePrestationAsync(
        Guid serviceId,
        UpsertServicePrestationRequest request,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var validationMessage = ValidatePrestationRequest(request);
        if (validationMessage is not null)
        {
            return AdminServiceCatalogOperationResult<ServicePrestationSummaryResponse>.ValidationFailed(validationMessage);
        }

        var service = await db.Services
            .Include(item => item.Prestations)
            .FirstOrDefaultAsync(item => item.Id == serviceId, cancellationToken);
        if (service is null)
        {
            return AdminServiceCatalogOperationResult<ServicePrestationSummaryResponse>.NotFound("Service introuvable.");
        }

        var before = new
        {
            service.Id,
            service.Name,
            Prestations = service.Prestations
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Name)
                .Select(ToServicePrestationResponse)
                .ToList()
        };

        var prestation = service.AddPrestation(
            request.Name,
            request.Description,
            request.SortOrder,
            GetPriceMin(request),
            GetPriceMax(request),
            request.Currency,
            request.IllustrationUrl);

        var after = ToServicePrestationResponse(prestation);
        AddAuditLog(
            actor,
            auditContext,
            "AdminServicePrestationUpserted",
            nameof(ServicePrestation),
            after.Id,
            $"Prestation '{prestation.Name}' rattachee au service '{service.Name}'.",
            before,
            after);
        await db.SaveChangesAsync(cancellationToken);

        return AdminServiceCatalogOperationResult<ServicePrestationSummaryResponse>.Ok(
            $"Prestation '{prestation.Name}' rattachee au service '{service.Name}'.",
            after,
            before,
            after);
    }

    public async Task<AdminServiceCatalogOperationResult<ServicePrestationSummaryResponse>> UpdatePrestationAsync(
        Guid prestationId,
        UpsertServicePrestationRequest request,
        CancellationToken cancellationToken)
        => await UpdatePrestationAsync(prestationId, request, null, null, cancellationToken);

    public async Task<AdminServiceCatalogOperationResult<ServicePrestationSummaryResponse>> UpdatePrestationAsync(
        Guid prestationId,
        UpsertServicePrestationRequest request,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var validationMessage = ValidatePrestationRequest(request);
        if (validationMessage is not null)
        {
            return AdminServiceCatalogOperationResult<ServicePrestationSummaryResponse>.ValidationFailed(validationMessage);
        }

        var prestation = await db.ServicePrestations.FirstOrDefaultAsync(item => item.Id == prestationId, cancellationToken);
        if (prestation is null)
        {
            return AdminServiceCatalogOperationResult<ServicePrestationSummaryResponse>.NotFound("Prestation introuvable.");
        }

        var before = ToServicePrestationResponse(prestation);
        prestation.Rename(request.Name, request.Description);
        prestation.MoveTo(request.SortOrder);
        prestation.UpdatePriceRange(GetPriceMin(request), GetPriceMax(request), request.Currency);
        prestation.UpdateIllustration(request.IllustrationUrl);

        var after = ToServicePrestationResponse(prestation);
        AddAuditLog(
            actor,
            auditContext,
            "AdminServicePrestationUpdated",
            nameof(ServicePrestation),
            after.Id,
            "Prestation de service modifiee.",
            before,
            after);
        await db.SaveChangesAsync(cancellationToken);

        return AdminServiceCatalogOperationResult<ServicePrestationSummaryResponse>.Ok(
            "Prestation de service modifiee.",
            after,
            before,
            after);
    }

    public Task<AdminServiceCatalogOperationResult<ServicePrestationSummaryResponse>> ActivatePrestationAsync(
        Guid prestationId,
        CancellationToken cancellationToken)
    {
        return ActivatePrestationAsync(prestationId, null, null, cancellationToken);
    }

    public Task<AdminServiceCatalogOperationResult<ServicePrestationSummaryResponse>> ActivatePrestationAsync(
        Guid prestationId,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        return SetPrestationActiveStateAsync(prestationId, isActive: true, actor, auditContext, cancellationToken);
    }

    public Task<AdminServiceCatalogOperationResult<ServicePrestationSummaryResponse>> DeactivatePrestationAsync(
        Guid prestationId,
        CancellationToken cancellationToken)
    {
        return DeactivatePrestationAsync(prestationId, null, null, cancellationToken);
    }

    public Task<AdminServiceCatalogOperationResult<ServicePrestationSummaryResponse>> DeactivatePrestationAsync(
        Guid prestationId,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        return SetPrestationActiveStateAsync(prestationId, isActive: false, actor, auditContext, cancellationToken);
    }

    private async Task<AdminServiceCatalogOperationResult<ServiceSummaryResponse>> SetServiceActiveStateAsync(
        Guid serviceId,
        bool isActive,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var service = await db.Services
            .Include(item => item.Prestations)
            .FirstOrDefaultAsync(item => item.Id == serviceId, cancellationToken);
        if (service is null)
        {
            return AdminServiceCatalogOperationResult<ServiceSummaryResponse>.NotFound("Service introuvable.");
        }

        var before = ToServiceResponse(service);
        if (isActive)
        {
            service.Activate();
        }
        else
        {
            service.Deactivate();
        }

        var after = ToServiceResponse(service);
        var message = isActive ? "Service active dans le catalogue." : "Service desactive dans le catalogue.";
        AddAuditLog(
            actor,
            auditContext,
            isActive ? "AdminServiceActivated" : "AdminServiceDeactivated",
            nameof(Service),
            after.Id,
            message,
            before,
            after);
        await db.SaveChangesAsync(cancellationToken);

        return AdminServiceCatalogOperationResult<ServiceSummaryResponse>.Ok(
            message,
            after,
            before,
            after);
    }

    private async Task<AdminServiceCatalogOperationResult<ServicePrestationSummaryResponse>> SetPrestationActiveStateAsync(
        Guid prestationId,
        bool isActive,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var prestation = await db.ServicePrestations.FirstOrDefaultAsync(item => item.Id == prestationId, cancellationToken);
        if (prestation is null)
        {
            return AdminServiceCatalogOperationResult<ServicePrestationSummaryResponse>.NotFound("Prestation introuvable.");
        }

        var before = ToServicePrestationResponse(prestation);
        if (isActive)
        {
            prestation.Activate();
        }
        else
        {
            prestation.Deactivate();
        }

        var after = ToServicePrestationResponse(prestation);
        var message = isActive ? "Prestation de service activee." : "Prestation de service desactivee.";
        AddAuditLog(
            actor,
            auditContext,
            isActive ? "AdminServicePrestationActivated" : "AdminServicePrestationDeactivated",
            nameof(ServicePrestation),
            after.Id,
            message,
            before,
            after);
        await db.SaveChangesAsync(cancellationToken);

        return AdminServiceCatalogOperationResult<ServicePrestationSummaryResponse>.Ok(
            message,
            after,
            before,
            after);
    }

    private static string? ValidateServiceRequest(UpsertServiceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Le nom du service est obligatoire.";
        }

        return null;
    }

    private static string? ValidatePrestationRequest(UpsertServicePrestationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Le nom de la prestation est obligatoire.";
        }

        return null;
    }

    private static int GetPriceMin(UpsertServiceRequest request)
    {
        return request.PriceMinAmount ?? request.NormalPriceAmount;
    }

    private static int GetPriceMax(UpsertServiceRequest request)
    {
        return request.PriceMaxAmount ?? request.PremiumPriceAmount;
    }

    private static int GetPriceMin(UpsertServicePrestationRequest request)
    {
        return request.PriceMinAmount ?? request.NormalPriceAmount;
    }

    private static int GetPriceMax(UpsertServicePrestationRequest request)
    {
        return request.PriceMaxAmount ?? request.PremiumPriceAmount;
    }

    private static ServiceSummaryResponse ToServiceResponse(Service service)
    {
        return new ServiceSummaryResponse(
            service.Id,
            service.Name,
            service.Description,
            service.IconName,
            service.Status.ToString(),
            service.IsActive,
            service.NormalPriceAmount,
            service.PremiumPriceAmount,
            service.Currency,
            service.Prestations
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Name)
                .Select(ToServicePrestationResponse)
                .ToList(),
            service.PriceMinAmount,
            service.PriceMaxAmount,
            service.IconUrl,
            service.ImageUrl);
    }

    private static ServicePrestationSummaryResponse ToServicePrestationResponse(ServicePrestation prestation)
    {
        return new ServicePrestationSummaryResponse(
            prestation.Id,
            prestation.Name,
            prestation.Description,
            prestation.SortOrder,
            prestation.NormalPriceAmount,
            prestation.PremiumPriceAmount,
            prestation.Currency,
            prestation.IsActive,
            prestation.PriceMinAmount,
            prestation.PriceMaxAmount,
            prestation.IllustrationUrl);
    }

    private void AddAuditLog(
        AuditActor? actor,
        AuditRequestContext? auditContext,
        string action,
        string entityType,
        Guid entityId,
        string summary,
        object? before,
        object? after)
    {
        if (actor is null)
        {
            return;
        }

        db.AuditLogEntries.Add(AuditLogFactory.Create(
            actor,
            action,
            entityType,
            entityId,
            summary,
            auditContext,
            before,
            after));
    }
}

public sealed record AdminServiceCatalogOperationResult<T>(
    AdminServiceCatalogOperationStatus Status,
    string Message,
    T? Response,
    object? Before = null,
    object? After = null)
{
    public bool IsSuccess => Status == AdminServiceCatalogOperationStatus.Ok;

    public static AdminServiceCatalogOperationResult<T> Ok(string message, T response, object? before, object? after)
        => new(AdminServiceCatalogOperationStatus.Ok, message, response, before, after);

    public static AdminServiceCatalogOperationResult<T> NotFound(string message)
        => new(AdminServiceCatalogOperationStatus.NotFound, message, default);

    public static AdminServiceCatalogOperationResult<T> ValidationFailed(string message)
        => new(AdminServiceCatalogOperationStatus.ValidationFailed, message, default);

    public static AdminServiceCatalogOperationResult<T> Conflict(string message)
        => new(AdminServiceCatalogOperationStatus.Conflict, message, default);
}

public enum AdminServiceCatalogOperationStatus
{
    Ok,
    NotFound,
    ValidationFailed,
    Conflict
}
