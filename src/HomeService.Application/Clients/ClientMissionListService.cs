using HomeService.Application.Abstractions;
using HomeService.Contracts.Clients;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Clients;

public sealed class ClientMissionListService(IAppDbContext db)
{
    public async Task<ClientMissionListResult> ListAsync(
        Guid? customerId,
        string? phoneNumber,
        string? status,
        CancellationToken cancellationToken)
    {
        var customer = await ResolveCustomerAsync(customerId, phoneNumber, cancellationToken);
        if (customer is null)
        {
            return ClientMissionListResult.NotFound("Client introuvable.");
        }

        var query = db.Missions
            .AsNoTracking()
            .Include(mission => mission.ServicePrestation)
            .Include(mission => mission.ServiceOption)
            .Where(mission => mission.CustomerId == customer.Id);

        var normalizedStatus = status?.Trim();
        if (string.Equals(normalizedStatus, "Active", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedStatus, "InProgress", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(mission => mission.Status != MissionStatus.Completed
                && mission.Status != MissionStatus.Cancelled
                && mission.Status != MissionStatus.Resolved);
        }
        else if (string.Equals(normalizedStatus, "Past", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(mission => mission.Status == MissionStatus.Completed
                || mission.Status == MissionStatus.Resolved);
        }
        else if (string.Equals(normalizedStatus, "Canceled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(mission => mission.Status == MissionStatus.Cancelled);
        }
        else if (!string.IsNullOrWhiteSpace(normalizedStatus)
            && Enum.TryParse<MissionStatus>(normalizedStatus, true, out var parsedStatus))
        {
            query = query.Where(mission => mission.Status == parsedStatus);
        }

        var serviceIds = await query
            .Select(mission => mission.ServiceId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var services = await db.Services
            .AsNoTracking()
            .Where(service => serviceIds.Contains(service.Id))
            .ToDictionaryAsync(service => service.Id, cancellationToken);

        var missions = await query
            .OrderByDescending(mission => mission.CreatedAt)
            .Take(80)
            .ToListAsync(cancellationToken);

        var providerIds = missions
            .Where(mission => mission.ProviderId.HasValue)
            .Select(mission => mission.ProviderId!.Value)
            .Distinct()
            .ToList();
        var providers = await db.Providers
            .AsNoTracking()
            .Where(provider => providerIds.Contains(provider.Id))
            .ToDictionaryAsync(provider => provider.Id, cancellationToken);
        var providerPhotoRows = await db.ProviderDocuments
            .AsNoTracking()
            .Where(document => providerIds.Contains(document.ProviderId)
                && document.DocumentType == ProviderDocumentType.Photo)
            .OrderByDescending(document => document.UpdatedAt ?? document.CreatedAt)
            .Select(document => new { document.ProviderId, document.StoragePath })
            .ToListAsync(cancellationToken);
        var providerPhotos = providerPhotoRows
            .GroupBy(document => document.ProviderId)
            .ToDictionary(group => group.Key, group => group.First().StoragePath);

        var rows = missions.Select(mission =>
        {
            services.TryGetValue(mission.ServiceId, out var service);
            var provider = mission.ProviderId is { } providerId
                && providers.TryGetValue(providerId, out var assignedProvider)
                    ? assignedProvider
                    : null;
            var providerPhoto = mission.ProviderId is { } photoProviderId
                && providerPhotos.TryGetValue(photoProviderId, out var photoPath)
                    ? photoPath
                    : null;
            return new ClientMissionListItemResponse(
                mission.Id,
                mission.MissionNumber,
                mission.Status.ToString(),
                mission.QuoteStatus.ToString(),
                mission.PaymentStatus.ToString(),
                service?.Name,
                mission.ServicePrestation?.Name,
                mission.ServiceOption?.Name,
                mission.ServiceAddress,
                mission.CreatedAt,
                mission.ScheduledFor,
                mission.FinalTotalAmount ?? mission.CompanyQuotedAmount ?? mission.EstimatedTotalAmount,
                mission.Currency,
                ResolvePrimaryAction(mission.Status, mission.PaymentStatus, mission.QuoteStatus),
                mission.ServicePrestation?.IllustrationUrl
                    ?? service?.IconUrl
                    ?? service?.ImageUrl,
                provider?.FullName,
                providerPhoto);
        }).ToList();

        return ClientMissionListResult.Ok(rows);
    }

    private async Task<Domain.Entities.CustomerProfile?> ResolveCustomerAsync(
        Guid? customerId,
        string? phoneNumber,
        CancellationToken cancellationToken)
    {
        if (customerId is not null)
        {
            return await db.Customers.AsNoTracking().FirstOrDefaultAsync(customer => customer.Id == customerId, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return null;
        }

        var phone = ClientAuthService.NormalizePhone(phoneNumber);
        return await db.Customers.AsNoTracking().FirstOrDefaultAsync(customer => customer.PhoneNumber == phone, cancellationToken);
    }

    private static string ResolvePrimaryAction(MissionStatus status, PaymentStatus paymentStatus, MissionQuoteStatus quoteStatus)
    {
        if (status == MissionStatus.Accepted
            && quoteStatus == MissionQuoteStatus.Submitted
            && paymentStatus == PaymentStatus.Pending)
        {
            return "AcceptQuote";
        }

        return status switch
        {
            MissionStatus.SearchingProvider or MissionStatus.Offered => "TrackRequest",
            MissionStatus.Accepted or MissionStatus.OnTheWay or MissionStatus.Started => "TrackMission",
            MissionStatus.Completed => "ValidateCompletion",
            MissionStatus.Cancelled => "ViewCancellation",
            MissionStatus.Disputed => "ViewDispute",
            _ => "Open"
        };
    }
}

public sealed record ClientMissionListResult(
    bool IsSuccess,
    IReadOnlyList<ClientMissionListItemResponse> Missions,
    string? Message)
{
    public static ClientMissionListResult Ok(IReadOnlyList<ClientMissionListItemResponse> missions)
        => new(true, missions, null);

    public static ClientMissionListResult NotFound(string message)
        => new(false, [], message);
}
