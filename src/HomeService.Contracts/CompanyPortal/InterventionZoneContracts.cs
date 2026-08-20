namespace HomeService.Contracts.CompanyPortal;

public sealed record InterventionZoneOptionResponse(
    string Code,
    string Commune,
    string Name,
    decimal Latitude,
    decimal Longitude);

public sealed record InterventionZoneCatalogResponse(
    IReadOnlyList<InterventionZoneOptionResponse> Zones,
    IReadOnlyList<string> SuggestedZoneCodes,
    IReadOnlyList<string> SelectedZoneCodes,
    int RadiusKm,
    bool IsCustomized,
    string Explanation);

public sealed record UpdateCompanyEmployeeZonesRequest(
    IReadOnlyList<string> ZoneCodes,
    int RadiusKm = 8);
