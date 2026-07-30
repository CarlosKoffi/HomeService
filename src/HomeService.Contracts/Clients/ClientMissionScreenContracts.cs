namespace HomeService.Contracts.Clients;

public sealed record ClientMissionScreenResponse(
    Guid MissionId,
    string MissionNumber,
    string Title,
    string Subtitle,
    string Status,
    string StatusLabel,
    string StatusTone,
    string Message,
    ClientMissionScreenPrimaryActionResponse PrimaryAction,
    ClientMissionScreenPriceResponse Price,
    ClientMissionScreenProviderResponse? Provider,
    ClientMissionScreenCompanyResponse? Company,
    IReadOnlyList<ClientMissionScreenTimelineStepResponse> Timeline,
    IReadOnlyList<ClientMissionAdditionalQuoteResponse> AdditionalQuotes,
    IReadOnlyList<ClientMissionAttachmentResponse> Photos);

public sealed record ClientMissionScreenPrimaryActionResponse(
    string? Code,
    string Label,
    bool IsEnabled,
    int? AmountToPayNow);

public sealed record ClientMissionScreenPriceResponse(
    int StartingPriceAmount,
    int MaximumPriceAmount,
    int? CurrentAmount,
    int? PartsEstimateAmount,
    string? PartsDescription,
    int PlatformCommissionAmount,
    int CompanyPayoutAmount,
    int TransportFeeAmount,
    string Currency,
    string Label);

public sealed record ClientMissionScreenProviderResponse(
    Guid ProviderId,
    string FullName,
    string? PhoneNumber,
    string? PhotoStoragePath,
    decimal? AverageRating,
    int CompletedMissionCount,
    int? EstimatedArrivalMinutes);

public sealed record ClientMissionScreenCompanyResponse(
    Guid CompanyId,
    string Name,
    string? PhoneNumber,
    string? Email);

public sealed record ClientMissionScreenTimelineStepResponse(
    string Code,
    string Label,
    string Description,
    string Status,
    DateTimeOffset? CompletedAt);
