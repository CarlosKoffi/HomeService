using System.Globalization;
using HomeService.Application.Abstractions;
using HomeService.Contracts.Services;
using HomeService.Domain.Common;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Clients;

public sealed class PublicServiceAvailabilityService(IAppDbContext db)
{
    private static readonly MissionStatus[] ClosedMissionStatuses =
    [
        MissionStatus.Completed,
        MissionStatus.Cancelled,
        MissionStatus.Disputed,
        MissionStatus.Resolved
    ];

    public async Task<PublicServiceAvailabilityResult> CheckAsync(
        PublicServiceAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ServiceId == Guid.Empty || string.IsNullOrWhiteSpace(request.Address))
        {
            return PublicServiceAvailabilityResult.Invalid("Sélectionnez un service et une adresse d’intervention.");
        }

        var service = await db.Services
            .AsNoTracking()
            .Where(item => item.Id == request.ServiceId && item.IsActive)
            .Select(item => new { item.Id, item.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (service is null)
        {
            return PublicServiceAvailabilityResult.NotFound("Ce service n’est plus disponible dans le catalogue.");
        }

        var mode = string.Equals(request.Mode, "Scheduled", StringComparison.OrdinalIgnoreCase)
            ? "Scheduled"
            : "Instant";
        if (mode == "Scheduled" && request.ScheduledFor is null)
        {
            return PublicServiceAvailabilityResult.Invalid("Choisissez la date et le créneau souhaités.");
        }

        var rawCandidates = await (
                from providerService in db.ProviderServices.AsNoTracking()
                join provider in db.Providers.AsNoTracking() on providerService.ProviderId equals provider.Id
                join company in db.Companies.AsNoTracking() on providerService.CompanyId equals company.Id
                where providerService.ServiceId == service.Id
                      && providerService.IsActive
                      && provider.Status == ProviderStatus.Approved
                      && company.Status == CompanyStatus.Approved
                select new Candidate(
                    provider.Id,
                    provider.IsAvailable,
                    company.InterventionZones))
            .ToListAsync(cancellationToken);

        var candidates = rawCandidates
            .Where(candidate => CoversRequestedZone(candidate.InterventionZones, request.Address))
            .GroupBy(candidate => candidate.ProviderId)
            .Select(group => group.First())
            .ToList();

        var availableCount = mode == "Scheduled"
            ? await CountScheduledCandidatesAsync(candidates, request.ScheduledFor!.Value, cancellationToken)
            : await CountImmediateCandidatesAsync(candidates, cancellationToken);

        var hasMatchingProfessionals = availableCount > 0;
        var slots = mode == "Scheduled"
            ? BuildSuggestedSlots(request.ScheduledFor!.Value)
            : [];

        var response = hasMatchingProfessionals
            ? mode == "Scheduled"
                ? new PublicServiceAvailabilityResponse(
                    service.Id,
                    service.Name,
                    "ScheduledAvailable",
                    true,
                    true,
                    availableCount,
                    "Ce créneau peut être demandé.",
                    "Des professionnels compatibles interviennent dans votre zone. Finalisez la demande dans l’application Wélé.",
                    slots)
                : new PublicServiceAvailabilityResponse(
                    service.Id,
                    service.Name,
                    "AvailableNow",
                    true,
                    true,
                    availableCount,
                    "Des professionnels sont disponibles.",
                    "Votre demande peut être lancée maintenant. L’application Wélé vous préviendra dès qu’un professionnel aura accepté.",
                    slots)
            : new PublicServiceAvailabilityResponse(
                service.Id,
                service.Name,
                "SearchRequired",
                true,
                false,
                0,
                "Nous allons élargir la recherche.",
                "Aucun professionnel ne peut être confirmé immédiatement, mais vous pouvez quand même envoyer votre demande dans l’application Wélé.",
                slots);

        return PublicServiceAvailabilityResult.Ok(response);
    }

    private async Task<int> CountImmediateCandidatesAsync(
        IReadOnlyCollection<Candidate> candidates,
        CancellationToken cancellationToken)
    {
        var providerIds = candidates
            .Where(candidate => candidate.IsAvailable)
            .Select(candidate => candidate.ProviderId)
            .ToArray();
        if (providerIds.Length == 0)
        {
            return 0;
        }

        var busyProviderIds = await db.Missions
            .AsNoTracking()
            .Where(mission => mission.ProviderId.HasValue
                && providerIds.Contains(mission.ProviderId.Value)
                && !ClosedMissionStatuses.Contains(mission.Status))
            .Select(mission => mission.ProviderId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        return providerIds.Except(busyProviderIds).Count();
    }

    private async Task<int> CountScheduledCandidatesAsync(
        IReadOnlyCollection<Candidate> candidates,
        DateTimeOffset scheduledFor,
        CancellationToken cancellationToken)
    {
        var providerIds = candidates.Select(candidate => candidate.ProviderId).ToArray();
        if (providerIds.Length == 0)
        {
            return 0;
        }

        var start = scheduledFor.AddMinutes(-29);
        var end = scheduledFor.AddMinutes(29);
        var busyProviderIds = await db.Missions
            .AsNoTracking()
            .Where(mission => mission.ProviderId.HasValue
                && providerIds.Contains(mission.ProviderId.Value)
                && !ClosedMissionStatuses.Contains(mission.Status)
                && (mission.ScheduledFor == null
                    || (mission.ScheduledFor >= start && mission.ScheduledFor <= end)))
            .Select(mission => mission.ProviderId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        return providerIds.Except(busyProviderIds).Count();
    }

    private static IReadOnlyList<PublicAvailabilitySlotResponse> BuildSuggestedSlots(DateTimeOffset requested)
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        return Enumerable.Range(0, 3)
            .Select(offset => requested.AddMinutes(offset * 30))
            .Select(start => new PublicAvailabilitySlotResponse(
                start,
                start.AddMinutes(30),
                $"{culture.TextInfo.ToTitleCase(start.ToString("ddd d MMM", culture))} · {start:HH:mm}–{start.AddMinutes(30):HH:mm}"))
            .ToList();
    }

    private static bool CoversRequestedZone(string? interventionZones, string address)
    {
        if (string.IsNullOrWhiteSpace(interventionZones))
        {
            return false;
        }

        var normalizedZones = CatalogNameNormalizer.Normalize(interventionZones);
        var normalizedAddress = CatalogNameNormalizer.Normalize(address);
        return normalizedAddress
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(part => part.Length >= 4 && normalizedZones.Contains(part, StringComparison.Ordinal));
    }

    private sealed record Candidate(Guid ProviderId, bool IsAvailable, string? InterventionZones);
}

public sealed record PublicServiceAvailabilityResult(
    PublicServiceAvailabilityStatus Status,
    string Message,
    PublicServiceAvailabilityResponse? Response = null)
{
    public bool IsSuccess => Status == PublicServiceAvailabilityStatus.Ok;

    public static PublicServiceAvailabilityResult Ok(PublicServiceAvailabilityResponse response)
        => new(PublicServiceAvailabilityStatus.Ok, response.Message, response);

    public static PublicServiceAvailabilityResult Invalid(string message)
        => new(PublicServiceAvailabilityStatus.Invalid, message);

    public static PublicServiceAvailabilityResult NotFound(string message)
        => new(PublicServiceAvailabilityStatus.NotFound, message);
}

public enum PublicServiceAvailabilityStatus
{
    Ok,
    Invalid,
    NotFound
}
