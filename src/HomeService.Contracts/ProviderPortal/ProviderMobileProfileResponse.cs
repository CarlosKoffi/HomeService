namespace HomeService.Contracts.ProviderPortal;

public sealed record ProviderMobileProfileResponse(
    Guid ProviderId,
    string FirstName,
    string LastName,
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
    decimal? MissionLatitude,
    decimal? MissionLongitude,
    string? ProfilePhotoUrl,
    bool CanViewPrices,
    ProviderMobileProfileCompletionResponse? ProfileCompletion,
    IReadOnlyList<ProviderMobileProfileServiceResponse> Services,
    IReadOnlyList<ProviderMobileProfileDocumentResponse> Documents,
    IReadOnlyList<ProviderMobilePortfolioItemResponse> PortfolioItems);

public sealed record ProviderMobileProfileServiceResponse(
    Guid ProviderServiceId,
    Guid ServiceId,
    string ServiceName,
    string IconName,
    string ExperienceLevel,
    int YearsOfExperience,
    string? PriceTier,
    bool RequiresPortfolio,
    int MinimumPortfolioItems,
    int PortfolioPhotoCount,
    bool CanReceiveMissions,
    IReadOnlyList<ProviderMobileProfilePrestationResponse> Prestations);

public sealed record ProviderMobileProfilePrestationResponse(
    Guid ServicePrestationId,
    string Name,
    int? PriceMinAmount,
    int? PriceMaxAmount,
    string? Currency);

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

public sealed record ProviderMobilePortfolioItemResponse(
    Guid Id,
    Guid ServiceId,
    string ServiceName,
    string OriginalFileName,
    string ContentType,
    string Status,
    string PreviewUrl);

public sealed record UpdateProviderMobileProfileRequest(
    string FirstName,
    string LastName,
    string? Email,
    string Address,
    int MissionRadiusKm,
    decimal? MissionLatitude = null,
    decimal? MissionLongitude = null);
