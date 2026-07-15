namespace HomeService.Contracts.CompanyPortal;

public sealed record CompanyPortalEmployeeServiceResponse(
    Guid ServiceId,
    string ServiceName,
    string ExperienceLevel,
    int YearsOfExperience,
    string PriceTier,
    int NormalPriceAmount,
    int PremiumPriceAmount,
    string Currency,
    bool IsActive,
    IReadOnlyList<CompanyPortalEmployeeServicePrestationResponse> Prestations);

public sealed record CompanyPortalEmployeeServicePrestationResponse(
    Guid ServicePrestationId,
    string PrestationName,
    int NormalPriceAmount,
    int PremiumPriceAmount,
    string Currency,
    bool IsActive);
