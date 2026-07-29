namespace HomeService.Contracts.ProviderPortal;

public sealed record ProviderMobileProfileResponse(
    Guid ProviderId,
    string FullName,
    string PhoneNumber,
    string? Email,
    string CompanyName,
    string Status,
    string EmploymentType,
    bool IsApprovedForMissions,
    bool IsAvailable,
    int MissionRadiusKm,
    string Address,
    ProviderMobileProfileCompletionResponse? ProfileCompletion,
    IReadOnlyList<ProviderMobileProfileServiceResponse> Services,
    IReadOnlyList<ProviderMobileProfileDocumentResponse> Documents);

public sealed record ProviderMobileProfileServiceResponse(
    Guid ProviderServiceId,
    Guid ServiceId,
    string ServiceName,
    string IconName,
    string ExperienceLevel,
    int YearsOfExperience,
    string PriceTier,
    bool RequiresPortfolio,
    int MinimumPortfolioItems,
    int PortfolioPhotoCount,
    bool CanReceiveMissions,
    IReadOnlyList<ProviderMobileProfilePrestationResponse> Prestations);

public sealed record ProviderMobileProfilePrestationResponse(
    Guid ServicePrestationId,
    string Name,
    int PriceMinAmount,
    int PriceMaxAmount,
    string Currency);

public sealed record ProviderMobileProfileDocumentResponse(
    Guid Id,
    string Type,
    string OriginalFileName,
    string ContentType,
    string PreviewUrl);

public sealed record ProviderMobilePortfolioUploadResponse(
    Guid Id,
    Guid ServiceId,
    string OriginalFileName,
    string ContentType,
    string Status,
    string PreviewUrl);
