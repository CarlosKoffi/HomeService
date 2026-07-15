namespace HomeService.Contracts.CompanyPortal;

public sealed record CompanyEmployeeServiceResponse(
    Guid ServiceId,
    string ServiceName,
    string ExperienceLevel,
    int YearsOfExperience,
    string PriceTier,
    int NormalPriceAmount,
    int PremiumPriceAmount,
    string Currency,
    bool IsActive,
    IReadOnlyList<CompanyEmployeeServicePrestationResponse> Prestations);

public sealed record CompanyEmployeeServicePrestationResponse(
    Guid ServicePrestationId,
    string PrestationName,
    int NormalPriceAmount,
    int PremiumPriceAmount,
    string Currency,
    bool IsActive);
