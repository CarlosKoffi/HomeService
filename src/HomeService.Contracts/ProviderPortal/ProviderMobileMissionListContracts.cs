namespace HomeService.Contracts.ProviderPortal;

public sealed record ProviderMobileMissionListResponse(
    DateTimeOffset ServerTime,
    IReadOnlyList<ProviderMobileMissionSummaryResponse> Items);
