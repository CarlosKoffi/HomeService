using HomeService.Application.Abstractions;
using HomeService.Contracts.Notifications;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Notifications;

public sealed class MobileDeviceTokenService(IAppDbContext db)
{
    public async Task<MobileDeviceTokenResult> RegisterAsync(
        MobileDeviceOwnerType ownerType,
        Guid ownerId,
        RegisterMobileDeviceTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return MobileDeviceTokenResult.ValidationFailed("Token mobile obligatoire.");
        }

        var platform = ParsePlatform(request.Platform);
        var existing = await db.MobileDeviceTokens
            .FirstOrDefaultAsync(token => token.Token == request.Token.Trim(), cancellationToken);

        if (existing is null)
        {
            existing = new MobileDeviceToken(ownerType, ownerId, platform, request.Token, request.DeviceLabel);
            db.MobileDeviceTokens.Add(existing);
        }
        else if (existing.OwnerType != ownerType || existing.OwnerId != ownerId)
        {
            existing.Reassign(ownerType, ownerId, platform, request.DeviceLabel);
        }
        else
        {
            existing.Refresh(platform, request.DeviceLabel);
        }

        await db.SaveChangesAsync(cancellationToken);

        return MobileDeviceTokenResult.Ok(ToResponse(existing));
    }

    public async Task<int> DisableOwnerTokensAsync(
        MobileDeviceOwnerType ownerType,
        Guid ownerId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var tokens = await db.MobileDeviceTokens
            .Where(token => token.OwnerType == ownerType && token.OwnerId == ownerId && token.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.Disable(reason);
        }

        await db.SaveChangesAsync(cancellationToken);
        return tokens.Count;
    }

    private static MobileDevicePlatform ParsePlatform(string? platform)
    {
        return Enum.TryParse<MobileDevicePlatform>(platform, true, out var parsed)
            ? parsed
            : MobileDevicePlatform.Unknown;
    }

    private static MobileDeviceTokenResponse ToResponse(MobileDeviceToken token)
    {
        return new MobileDeviceTokenResponse(
            token.Id,
            token.OwnerType.ToString(),
            token.OwnerId,
            token.Platform.ToString(),
            token.IsActive,
            token.LastSeenAt);
    }
}

public sealed record MobileDeviceTokenResult(
    bool IsSuccess,
    MobileDeviceTokenResponse? Response,
    string? Message)
{
    public static MobileDeviceTokenResult Ok(MobileDeviceTokenResponse response)
        => new(true, response, null);

    public static MobileDeviceTokenResult ValidationFailed(string message)
        => new(false, null, message);
}
