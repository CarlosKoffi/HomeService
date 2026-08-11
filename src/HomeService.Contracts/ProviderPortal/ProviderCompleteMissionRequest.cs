namespace HomeService.Contracts.ProviderPortal;

public sealed record ProviderCompleteMissionRequest(
    int ActualDurationMinutes,
    string? Note,
    string? CompletionPhotoPath,
    string? QualityExceptionReason = null);
