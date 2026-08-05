namespace HomeService.Contracts.ProviderPortal;

public sealed record UpdateProviderMobileAvailabilityRequest(
    bool IsAvailable,
    decimal? Latitude,
    decimal? Longitude);

public sealed record ProviderMobileAvailabilityResponse(
    bool IsAvailable,
    string AvailabilityLabel,
    bool CanChangeAvailability,
    string Message);
