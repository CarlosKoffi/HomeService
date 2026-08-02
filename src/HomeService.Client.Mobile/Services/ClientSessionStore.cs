using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Services;

public sealed class ClientSessionStore
{
    private const string TokenKey = "ClientAuthToken";
    private const string ExpiresAtKey = "ClientAuthExpiresAt";
    private const string PhoneNumberKey = "ClientPhoneNumber";
    private const string DisplayNameKey = "ClientDisplayName";
    private const string PreviewModeKey = "ClientPreviewMode";

    public async Task SaveAsync(ClientAuthResponse response)
    {
        await SetTokenAsync(response.Token);
        Preferences.Default.Set(ExpiresAtKey, response.ExpiresAt.ToString("O"));
        Preferences.Default.Set(PhoneNumberKey, response.PhoneNumber);
        Preferences.Default.Set(DisplayNameKey, $"{response.FirstName} {response.LastName}".Trim());
    }

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(TokenKey);
        }
        catch
        {
            var fallbackToken = Preferences.Default.Get(TokenKey, string.Empty);
            return string.IsNullOrWhiteSpace(fallbackToken) ? null : fallbackToken;
        }
    }

    public string? GetPhoneNumber()
    {
        if (IsPreviewMode())
        {
            return "+2250700000000";
        }

        var phoneNumber = Preferences.Default.Get(PhoneNumberKey, string.Empty);
        return string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber;
    }

    public string GetDisplayName()
    {
        if (IsPreviewMode())
        {
            return "Carlos";
        }

        return Preferences.Default.Get(DisplayNameKey, "Client Wele");
    }

    public bool HasSession()
    {
        return IsPreviewMode() || !string.IsNullOrWhiteSpace(Preferences.Default.Get(PhoneNumberKey, string.Empty));
    }

    public async Task<bool> HasValidSessionAsync()
    {
        if (IsPreviewMode())
        {
            return true;
        }

        var phoneNumber = Preferences.Default.Get(PhoneNumberKey, string.Empty);
        var token = await GetTokenAsync();
        var expiresAtValue = Preferences.Default.Get(ExpiresAtKey, string.Empty);

        if (string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        return DateTimeOffset.TryParse(expiresAtValue, out var expiresAt)
            && expiresAt > DateTimeOffset.UtcNow.AddMinutes(1);
    }

    public bool IsPreviewMode()
    {
        return Preferences.Default.Get(PreviewModeKey, false);
    }

    public Task StartPreviewAsync()
    {
        Preferences.Default.Set(PreviewModeKey, true);
        Preferences.Default.Set(DisplayNameKey, "Carlos");
        return Task.CompletedTask;
    }

    public async Task ClearAsync()
    {
        try
        {
            SecureStorage.Default.Remove(TokenKey);
        }
        catch
        {
            Preferences.Default.Remove(TokenKey);
        }

        Preferences.Default.Remove(ExpiresAtKey);
        Preferences.Default.Remove(PhoneNumberKey);
        Preferences.Default.Remove(DisplayNameKey);
        Preferences.Default.Remove(PreviewModeKey);
        await Task.CompletedTask;
    }

    private static async Task SetTokenAsync(string token)
    {
        try
        {
            await SecureStorage.Default.SetAsync(TokenKey, token);
        }
        catch
        {
            Preferences.Default.Set(TokenKey, token);
        }
    }
}
