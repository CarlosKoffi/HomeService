using HomeService.Contracts.Clients;

namespace HomeService.Client.Mobile.Services;

public sealed class ClientSessionStore
{
    private const string TokenKey = "ClientAuthToken";
    private const string ExpiresAtKey = "ClientAuthExpiresAt";
    private const string PhoneNumberKey = "ClientPhoneNumber";
    private const string DisplayNameKey = "ClientDisplayName";

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
        var phoneNumber = Preferences.Default.Get(PhoneNumberKey, string.Empty);
        return string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber;
    }

    public string GetDisplayName()
    {
        return Preferences.Default.Get(DisplayNameKey, "Client wélé");
    }

    public bool HasSession()
    {
        return !string.IsNullOrWhiteSpace(Preferences.Default.Get(PhoneNumberKey, string.Empty));
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
