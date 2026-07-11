namespace HomeService.Contracts.ProviderPortal;

public sealed record ProviderCompanySearchResponse(
    Guid CompanyId,
    string CompanyName,
    string? City,
    int MatchingServiceCount,
    IReadOnlyList<string> MatchingServices,
    double? DistanceKm,
    string Status);
