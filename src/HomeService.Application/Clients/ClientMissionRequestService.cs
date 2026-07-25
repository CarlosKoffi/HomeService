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

        var customer = await FindOrCreateCustomerAsync(request, cancellationToken);
        var mission = new Mission(
            customer.Id,
            request.ServiceId,
            mode,
            paymentMethod,
            request.ScheduledFor,
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
        await db.SaveChangesAsync(cancellationToken);

        var dispatchResult = await dispatchService.CreateInitialOffersAsync(
            mission.Id,
            request.IsUrgent || mode == MissionMode.Instant,
            cancellationToken);

        if (dispatchResult.IsSuccess && dispatchResult.Offers.Count > 0)
        {
            mission.MarkCompanyOffersSent();
            await db.SaveChangesAsync(cancellationToken);
        }

        var message = dispatchResult.IsSuccess && dispatchResult.Offers.Count > 0
            ? "Votre demande a ete transmise aux entreprises disponibles."
            : "Votre demande est enregistree. Nous recherchons une entreprise disponible.";

        return ClientMissionCreationResult.Ok(new CreateClientMissionResponse(
            mission.Id,
            mission.MissionNumber,
            mission.Status.ToString(),
            dispatchResult.Offers.Count,
            mission.CreatedAt,
            message));
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

        return errors;
    }
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
