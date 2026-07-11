namespace HomeService.Contracts.ProviderPortal;

public sealed record ProviderAcceptMissionRequest(
    decimal? Latitude,
    decimal? Longitude,
    int? AccuracyMeters);
