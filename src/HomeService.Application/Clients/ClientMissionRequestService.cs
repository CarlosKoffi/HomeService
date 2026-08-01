using HomeService.Application.Abstractions;
using HomeService.Application.Missions;
using HomeService.Contracts.Clients;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Clients;

public sealed class ClientMissionRequestService(
    IAppDbContext db,
    MissionDispatchService dispatchService)
{
    public async Task<ClientMissionCreationResult> CreateAsync(
        CreateClientMissionRequest request,
        CancellationToken cancellationToken)
    {
        var validationErrors = Validate(request);
        if (validationErrors.Count > 0)
        {
            return ClientMissionCreationResult.ValidationFailed(validationErrors);
        }

        var service = await db.Services
            .AsNoTracking()
            .Include(item => item.Prestations)
            .FirstOrDefaultAsync(item => item.Id == request.ServiceId && item.IsActive, cancellationToken);
        if (service is null)
        {
            return ClientMissionCreationResult.ValidationFailed(["Service introuvable ou inactif."]);
        }

        if (request.ServicePrestationId.HasValue
            && !service.Prestations.Any(prestation => prestation.Id == request.ServicePrestationId.Value && prestation.IsActive))
        {
            return ClientMissionCreationResult.ValidationFailed(["La prestation choisie ne correspond pas au service."]);
        }

        if (!Enum.TryParse<MissionMode>(request.Mode, true, out var mode))
        {
            return ClientMissionCreationResult.ValidationFailed(["Mode de mission invalide."]);
        }

        if (!Enum.TryParse<PaymentMethod>(request.PaymentMethod, true, out var paymentMethod))
        {
            return ClientMissionCreationResult.ValidationFailed(["Mode de paiement invalide."]);
        }

        if (paymentMethod == PaymentMethod.Cash)
        {
            return ClientMissionCreationResult.ValidationFailed(["Le paiement cash n'est pas active au demarrage."]);
        }

        if (mode == MissionMode.Scheduled && request.ScheduledFor is null)
        {
            return ClientMissionCreationResult.ValidationFailed(["La date du rendez-vous est obligatoire pour une mission programmee."]);
        }

        if (mode == MissionMode.Scheduled && request.ScheduledFor <= DateTimeOffset.UtcNow.AddMinutes(15))
        {
            return ClientMissionCreationResult.ValidationFailed(["Choisissez un rendez-vous au moins 15 minutes dans le futur."]);
        }

        var scheduledFor = request.ScheduledFor?.ToUniversalTime();

        var customer = await FindOrCreateCustomerAsync(request, cancellationToken);
        var activeMissions = await db.Missions
                .AsNoTracking()
                .Where(item => item.CustomerId == customer.Id && item.Mode == mode)
                .Where(item => item.Status == MissionStatus.Created
                    || item.Status == MissionStatus.SearchingProvider
                    || item.Status == MissionStatus.Offered
                    || item.Status == MissionStatus.Assigned
                    || item.Status == MissionStatus.Accepted
                    || item.Status == MissionStatus.OnTheWay
                    || item.Status == MissionStatus.Started)
                .Select(item => new ExistingClientMission(
                    item.MissionNumber,
                    item.ServiceId,
                    item.ServicePrestationId,
                    item.Mode,
                    item.Status,
                    item.ServiceAddress,
                    item.ScheduledFor))
                .ToListAsync(cancellationToken);

        var duplicate = activeMissions.FirstOrDefault(item => ClientMissionDuplicatePolicy.IsDuplicate(
            request.ServiceId,
            request.ServicePrestationId,
            request.ServiceAddress,
            mode,
            scheduledFor,
            item));
        if (duplicate is not null)
        {
            var requestType = mode == MissionMode.Instant ? "demande immediate" : "demande au meme horaire";
            return ClientMissionCreationResult.ValidationFailed(
            [
                $"Une {requestType} est deja en cours pour ce besoin a cette adresse ({duplicate.MissionNumber}). Consultez-la dans Mes demandes."
            ]);
        }

        var mission = new Mission(
            customer.Id,
            request.ServiceId,
            mode,
            paymentMethod,
            scheduledFor,
            Math.Clamp(request.EstimatedDurationMinutes, 30, 720),
            request.ServicePrestationId,
            request.Description,
            request.RequiresCompanyQuote);

        mission.SetServiceLocation(
            request.ServiceAddress,
            request.ServiceLatitude,
            request.ServiceLongitude);
        mission.StartCompanySearch();

        db.Missions.Add(mission);
        AddCustomerPhotos(mission, request);
        await db.SaveChangesAsync(cancellationToken);

        var urgentOptionEnabled = await MissionWorkflowSettingsResolver.ResolveFlagAsync(
            db,
            MissionWorkflowSettingsResolver.UrgentMissionsEnabled,
            fallbackValue: false,
            cancellationToken);
        var isUrgent = urgentOptionEnabled
            && mode == MissionMode.Instant
            && request.IsUrgent;

        var dispatchResult = await dispatchService.CreateInitialOffersAsync(
            mission.Id,
            isUrgent,
            cancellationToken);

        if (dispatchResult.IsSuccess && dispatchResult.Offers.Count > 0)
        {
            mission.MarkCompanyOffersSent();
            await db.SaveChangesAsync(cancellationToken);
        }

        var message = dispatchResult.IsSuccess && dispatchResult.Offers.Count > 0
            ? "Votre demande a ete transmise aux entreprises disponibles."
            : "Votre demande est enregistree. Nous recherchons une entreprise disponible.";

        var priceRange = ResolvePriceRange(service, request.ServicePrestationId);
        return ClientMissionCreationResult.Ok(new CreateClientMissionResponse(
            mission.Id,
            mission.MissionNumber,
            mission.Status.ToString(),
            dispatchResult.Offers.Count,
            priceRange.PriceMinAmount,
            priceRange.PriceMaxAmount,
            priceRange.Currency,
            mission.CreatedAt,
            message));
    }

    private static ClientMissionRequestPriceRange ResolvePriceRange(Service service, Guid? servicePrestationId)
    {
        if (servicePrestationId.HasValue)
        {
            var prestation = service.Prestations.First(item => item.Id == servicePrestationId.Value);
            return new ClientMissionRequestPriceRange(
                prestation.PriceMinAmount,
                prestation.PriceMaxAmount,
                prestation.Currency);
        }

        return new ClientMissionRequestPriceRange(
            service.PriceMinAmount,
            service.PriceMaxAmount,
            service.Currency);
    }

    private async Task<CustomerProfile> FindOrCreateCustomerAsync(
        CreateClientMissionRequest request,
        CancellationToken cancellationToken)
    {
        var phone = request.PhoneNumber.Trim();
        var customer = await db.Customers
            .FirstOrDefaultAsync(item => item.PhoneNumber == phone, cancellationToken);
        if (customer is not null)
        {
            return customer;
        }

        customer = new CustomerProfile(request.FirstName, request.LastName, phone);
        db.Customers.Add(customer);
        return customer;
    }

    private void AddCustomerPhotos(Mission mission, CreateClientMissionRequest request)
    {
        if (request.Photos is null)
        {
            return;
        }

        foreach (var photo in request.Photos.Take(MaxCustomerPhotos))
        {
            db.MissionAttachments.Add(new MissionAttachment(
                mission.Id,
                MissionAttachmentType.CustomerPhoto,
                photo.OriginalFileName,
                photo.StoragePath,
                photo.ContentType,
                photo.FileSizeBytes,
                photo.Caption));
        }
    }

    private static List<string> Validate(CreateClientMissionRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            errors.Add("Le prenom client est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            errors.Add("Le nom client est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            errors.Add("Le numero de telephone client est obligatoire.");
        }

        if (request.ServiceId == Guid.Empty)
        {
            errors.Add("Le service est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(request.Mode))
        {
            errors.Add("Le mode de mission est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(request.PaymentMethod))
        {
            errors.Add("Le mode de paiement est obligatoire.");
        }

        if (request.EstimatedDurationMinutes <= 0)
        {
            errors.Add("La duree estimee doit etre positive.");
        }

        ValidatePhotos(request.Photos, errors);
        return errors;
    }

    private static void ValidatePhotos(IReadOnlyList<ClientMissionPhotoRequest>? photos, List<string> errors)
    {
        if (photos is null || photos.Count == 0)
        {
            return;
        }

        if (photos.Count > MaxCustomerPhotos)
        {
            errors.Add($"Ajoutez {MaxCustomerPhotos} photos maximum pour garder la demande legere.");
        }

        foreach (var photo in photos)
        {
            if (string.IsNullOrWhiteSpace(photo.OriginalFileName))
            {
                errors.Add("Chaque photo doit avoir un nom de fichier.");
            }

            if (string.IsNullOrWhiteSpace(photo.StoragePath))
            {
                errors.Add("Chaque photo doit avoir un chemin de stockage.");
            }

            if (string.IsNullOrWhiteSpace(photo.ContentType) || !photo.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Les pieces jointes client doivent etre des images.");
            }

            if (photo.FileSizeBytes <= 0)
            {
                errors.Add("Chaque photo doit avoir une taille valide.");
            }

            if (photo.FileSizeBytes > MaxCustomerPhotoBytes)
            {
                errors.Add("Chaque photo client doit rester inferieure a 5 Mo.");
            }
        }
    }

    private const int MaxCustomerPhotos = 5;
    private const long MaxCustomerPhotoBytes = 5 * 1024 * 1024;

    private sealed record ClientMissionRequestPriceRange(
        int PriceMinAmount,
        int PriceMaxAmount,
        string Currency);
}

public sealed record ClientMissionCreationResult(
    bool IsSuccess,
    IReadOnlyList<string> Errors,
    CreateClientMissionResponse? Response)
{
    public static ClientMissionCreationResult Ok(CreateClientMissionResponse response)
    {
        return new ClientMissionCreationResult(true, [], response);
    }

    public static ClientMissionCreationResult ValidationFailed(IReadOnlyList<string> errors)
    {
        return new ClientMissionCreationResult(false, errors, null);
    }
}
