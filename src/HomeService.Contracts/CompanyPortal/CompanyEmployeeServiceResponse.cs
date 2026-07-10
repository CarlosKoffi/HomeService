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
    bool IsActive);
