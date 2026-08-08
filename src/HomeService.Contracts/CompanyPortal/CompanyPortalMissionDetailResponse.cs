namespace HomeService.Contracts.CompanyPortal;

public sealed record CompanyPortalMissionDetailResponse(
    CompanyPortalMissionResponse Mission,
    string? PrestationName,
    string? OptionName,
    string? Description,
    DateTimeOffset CreatedAt,
    Guid? OfferId,
    string? OfferStatus,
    DateTimeOffset? OfferExpiresAt,
    bool CanAccept,
    bool CanRefuse,
    bool CanAssign,
    string? ProviderPhoneNumber,
    decimal? ProviderLatitude,
    decimal? ProviderLongitude,
    double? ProviderDistanceKilometers,
    IReadOnlyList<CompanyCustomerMissionHistoryResponse> CustomerHistory,
    string? ProviderPhotoUrl = null);

public sealed record CompanyCustomerMissionHistoryResponse(
    Guid MissionId,
    string MissionNumber,
    string ServiceName,
    string? PrestationName,
    string Status,
    DateTimeOffset Date,
    int? Rating);
