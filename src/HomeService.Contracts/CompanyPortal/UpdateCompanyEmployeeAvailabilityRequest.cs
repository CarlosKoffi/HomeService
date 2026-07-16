namespace HomeService.Contracts.CompanyPortal;

public sealed record UpdateCompanyEmployeeAvailabilityRequest(
    bool IsAvailable,
    decimal? Latitude = null,
    decimal? Longitude = null);
