namespace HomeService.Contracts.Services;

public sealed record PublicServiceAvailabilityRequest(
    Guid ServiceId,
    string Address,
    string Mode,
    DateTimeOffset? ScheduledFor = null);

public sealed record PublicServiceAvailabilityResponse(
    Guid ServiceId,
    string ServiceName,
    string Status,
    bool CanContinue,
    bool HasMatchingProfessionals,
    int MatchingProfessionalCount,
    string Headline,
    string Message,
    IReadOnlyList<PublicAvailabilitySlotResponse> SuggestedSlots);

public sealed record PublicAvailabilitySlotResponse(
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Label);
