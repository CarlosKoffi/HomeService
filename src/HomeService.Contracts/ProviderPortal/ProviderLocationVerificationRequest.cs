namespace HomeService.Contracts.ProviderPortal;

public sealed record ProviderLocationVerificationRequest(
    decimal? Latitude,
    decimal? Longitude,
    int? AccuracyMeters);
