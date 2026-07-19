namespace HomeService.Contracts.Services;

public sealed record ServiceCatalogInsightListResponse(
    IReadOnlyList<ServiceCatalogInsightResponse> Items,
    ServiceCatalogInsightTotalsResponse Totals);

public sealed record ServiceCatalogInsightTotalsResponse(
    int ServiceCount,
    int ActiveProviderCount,
    int InterimProviderCount,
    int MissionCount,
    int CompletedMissionCount,
    int RevenueAmount,
    string Currency);

public sealed record ServiceCatalogInsightResponse(
    Guid ServiceId,
    string ServiceName,
    int TotalPrestations,
    int ActivePrestations,
    int CompanyCount,
    int ActiveProviderCount,
    int InterimProviderCount,
    int MissionCount,
    int CompletedMissionCount,
    int DisputedMissionCount,
    int RevenueAmount,
    string Currency,
    IReadOnlyList<ServicePrestationCatalogInsightResponse> Prestations);

public sealed record ServicePrestationCatalogInsightResponse(
    Guid ServicePrestationId,
    string ServicePrestationName,
    int ActiveProviderCount,
    int MissionCount,
    int CompletedMissionCount,
    int RevenueAmount);
