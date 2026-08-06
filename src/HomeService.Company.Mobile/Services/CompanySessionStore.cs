using HomeService.Contracts.CompanyPortal;
using Microsoft.Maui.Storage;

namespace HomeService.Company.Mobile.Services;

public sealed class CompanySessionStore
{
    private const string TokenKey = "CompanyAccessToken";
    private const string CompanyIdKey = "CompanyId";
    private const string CompanyNameKey = "CompanyName";
    private const string UserNameKey = "CompanyUserName";

    public async Task SaveAsync(CompanyPortalLoginResponse response)
    {
        await SecureStorage.Default.SetAsync(TokenKey, response.Token);
        await SecureStorage.Default.SetAsync(CompanyIdKey, response.CompanyId.ToString("D"));
        await SecureStorage.Default.SetAsync(CompanyNameKey, response.CompanyName);
        await SecureStorage.Default.SetAsync(UserNameKey, response.UserName);
    }

    public Task<string?> GetTokenAsync() => SecureStorage.Default.GetAsync(TokenKey);

    public async Task<Guid?> GetCompanyIdAsync()
    {
        var value = await SecureStorage.Default.GetAsync(CompanyIdKey);
        return Guid.TryParse(value, out var companyId) ? companyId : null;
    }

    public Task<string?> GetCompanyNameAsync() => SecureStorage.Default.GetAsync(CompanyNameKey);
    public Task<string?> GetUserNameAsync() => SecureStorage.Default.GetAsync(UserNameKey);

    public async Task<bool> HasSessionAsync()
        => !string.IsNullOrWhiteSpace(await GetTokenAsync()) && (await GetCompanyIdAsync()).HasValue;

    public void Clear()
    {
        SecureStorage.Default.Remove(TokenKey);
        SecureStorage.Default.Remove(CompanyIdKey);
        SecureStorage.Default.Remove(CompanyNameKey);
        SecureStorage.Default.Remove(UserNameKey);
    }
}
