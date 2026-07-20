namespace HomeService.Contracts.ProviderPortal;

public sealed record ProviderMobileHomeResponse(
    ProviderMobileStatusResponse Status,
    ProviderMobileProfileCompletionResponse? ProfileCompletion,
    ProviderMobileMissionSummaryResponse? UpcomingMission,
    ProviderMobileMissionOfferResponse? LiveOffer);

public sealed record ProviderMobileStatusResponse(
    string DisplayName,
    string CompanyName,
    bool IsAvailable,
    string AvailabilityLabel,
    int MissionRadiusKm);

public sealed record ProviderMobileProfileCompletionResponse(
    int Percent,
    string Message,
    IReadOnlyList<string> MissingItems);

public sealed record ProviderMobileMissionSummaryResponse(
    Guid AssignmentId,
    Guid MissionId,
    string MissionNumber,
    string ServiceName,
    string ServiceIconName,
    string CompanyName,
    string LocationLabel,
    DateTimeOffset? ScheduledFor,
    string Status,
    bool CanCallCustomer,
    string? CustomerPhoneNumber);

public sealed record ProviderMobileMissionOfferResponse(
    Guid AssignmentId,
    Guid MissionId,
    string MissionNumber,
    string ServiceName,
    string ServiceIconName,
    string CompanyName,
    string CustomerDisplayName,
    string LocationLabel,
    double? DistanceKm,
    int? EstimatedTravelMinutes,
    DateTimeOffset ExpiresAt,
    int SecondsToRespond,
    string Instruction);
