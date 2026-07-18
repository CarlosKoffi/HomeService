namespace HomeService.Contracts.ProviderPortal;

public sealed record ProviderOnboardingOptionResponse(
    Guid Id,
    string Type,
    string Label,
    Guid ServiceId,
    string ServiceName,
    Guid? ServicePrestationId,
    string? PrestationName);

public sealed record ProviderCompanyOpportunityResponse(
    Guid CompanyId,
    string CompanyName,
    string? City,
    string? Address,
    IReadOnlyList<string> MatchingServices,
    IReadOnlyList<string> MatchingPrestations,
    string ProximityLabel,
    string Status);
