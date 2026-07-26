using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class MobileDeviceToken : AuditableEntity
{
    private MobileDeviceToken()
    {
    }

    public MobileDeviceToken(
        MobileDeviceOwnerType ownerType,
        Guid ownerId,
        MobileDevicePlatform platform,
        string token,
        string? deviceLabel)
    {
        OwnerType = ownerType;
        OwnerId = ownerId;
        Platform = platform;
        Token = CleanRequired(token, 4096);
        DeviceLabel = Clean(deviceLabel, 120);
        IsActive = true;
        LastSeenAt = DateTimeOffset.UtcNow;
    }

    public MobileDeviceOwnerType OwnerType { get; private set; }
    public Guid OwnerId { get; private set; }
    public MobileDevicePlatform Platform { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public string? DeviceLabel { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }
    public DateTimeOffset? DisabledAt { get; private set; }
    public string? FailureReason { get; private set; }

    public void Refresh(MobileDevicePlatform platform, string? deviceLabel)
    {
        Platform = platform;
        DeviceLabel = Clean(deviceLabel, 120);
        IsActive = true;
        DisabledAt = null;
        FailureReason = null;
        LastSeenAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void Disable(string? reason = null)
    {
        IsActive = false;
        DisabledAt = DateTimeOffset.UtcNow;
        FailureReason = Clean(reason, 500);
        Touch();
    }

    private static string CleanRequired(string value, int maxLength)
    {
        var cleaned = Clean(value, maxLength);
        if (cleaned is null)
        {
            throw new ArgumentException("La valeur obligatoire est vide.", nameof(value));
        }

        return cleaned;
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }
}
