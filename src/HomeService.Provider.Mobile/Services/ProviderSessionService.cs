using Microsoft.Maui.Storage;

namespace HomeService.Provider.Mobile.Services;

public sealed class ProviderSessionService
{
    private const string AccessTokenKey = "ProviderAccessToken";
    private const string ExpiresAtKey = "ProviderAccessTokenExpiresAt";
    private const string DisplayNameKey = "ProviderDisplayName";

    public async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            var expiresRaw = await SecureStorage.Default.GetAsync(ExpiresAtKey);
            if (DateTimeOffset.TryParse(expiresRaw, out var expiresAt) && expiresAt <= DateTimeOffset.UtcNow)
            {
                await ClearAsync();
                return null;
            }

            return await SecureStorage.Default.GetAsync(AccessTokenKey);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveAsync(string accessToken, DateTimeOffset expiresAt, string displayName)
    {
        await SecureStorage.Default.SetAsync(AccessTokenKey, accessToken);
        await SecureStorage.Default.SetAsync(ExpiresAtKey, expiresAt.ToString("O"));
        await SecureStorage.Default.SetAsync(DisplayNameKey, displayName);
        Preferences.Default.Remove(AccessTokenKey);
    }

    public async Task ClearAsync()
    {
        try
        {
            SecureStorage.Default.Remove(AccessTokenKey);
            SecureStorage.Default.Remove(ExpiresAtKey);
            SecureStorage.Default.Remove(DisplayNameKey);
        }
        catch
        {
            // Secure storage may be unavailable on an unsupported test platform.
        }

        Preferences.Default.Remove(AccessTokenKey);
        await Task.CompletedTask;
    }
}
