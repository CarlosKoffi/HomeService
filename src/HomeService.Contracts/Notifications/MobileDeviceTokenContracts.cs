namespace HomeService.Contracts.Notifications;

public sealed record RegisterMobileDeviceTokenRequest(
    string Token,
    string Platform,
    string? DeviceLabel);

public sealed record MobileDeviceTokenResponse(
    Guid Id,
    string OwnerType,
    Guid OwnerId,
    string Platform,
    bool IsActive,
    DateTimeOffset LastSeenAt);
