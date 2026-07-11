namespace HomeService.Contracts.ProviderPortal;

public sealed record ProviderInvitationPreviewResponse(
    Guid InvitationId,
    Guid ProviderId,
    string Code,
    string ProviderName,
    string PhoneNumber,
    string CompanyName,
    string Status,
    DateTimeOffset ExpiresAt);

public sealed record ProviderInvitationActivationRequest(
    string Code,
    string Password,
    string ConfirmPassword,
    bool RememberMe);

public sealed record ProviderPortalLoginRequest(
    string PhoneNumber,
    string Password,
    bool RememberMe);

public sealed record ProviderPortalLoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    Guid ProviderId,
    string DisplayName,
    string PhoneNumber,
    string? CompanyName,
    string Status,
    bool CanReceiveMissions);

public sealed record ProviderPortalMeResponse(
    Guid ProviderId,
    string DisplayName,
    string PhoneNumber,
    string? CompanyName,
    string Status,
    bool CanReceiveMissions,
    bool IsAvailable);
