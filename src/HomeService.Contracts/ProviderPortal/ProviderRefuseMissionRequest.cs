namespace HomeService.Contracts.ProviderPortal;

public sealed record ProviderRefuseMissionRequest(
    string Reason,
    string? Comment);
