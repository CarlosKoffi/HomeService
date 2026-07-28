using HomeService.Contracts.Admin;
using Microsoft.JSInterop;

namespace HomeService.Admin.Services;

public sealed class AdminSessionState(
    PlatformApiClient apiClient,
    AdminApiSessionAccessor sessionAccessor,
    IJSRuntime jsRuntime)
{
    private const string StorageKey = "wele-admin-session";
    private const string CookieName = "wele-admin-session";
    private bool isInitialized;

    public AdminCurrentUserResponse? CurrentUser { get; private set; }
    public string? Token => sessionAccessor.Token;
    public bool IsAuthenticated => CurrentUser is not null && !string.IsNullOrWhiteSpace(sessionAccessor.Token);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (isInitialized)
        {
            return;
        }

        isInitialized = true;
        var token = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", cancellationToken, StorageKey);
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        sessionAccessor.Token = token;
        try
        {
            CurrentUser = await apiClient.GetCurrentAdminAsync(cancellationToken);
            if (CurrentUser is null)
            {
                await ClearAsync(cancellationToken);
            }
        }
        catch
        {
            await ClearAsync(cancellationToken);
        }
    }

    public async Task<bool> SignInAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        AdminLoginResponse? response;
        try
        {
            response = await apiClient.LoginAdminAsync(new AdminLoginRequest(email, password), cancellationToken);
        }
        catch (PlatformApiException)
        {
            return false;
        }

        if (response is null)
        {
            return false;
        }

        sessionAccessor.Token = response.Token;
        CurrentUser = response.User;
        await jsRuntime.InvokeVoidAsync("localStorage.setItem", cancellationToken, StorageKey, response.Token);
        await jsRuntime.InvokeVoidAsync(
            "eval",
            cancellationToken,
            $"document.cookie = '{CookieName}=' + encodeURIComponent('{response.Token}') + '; path=/; max-age=28800; samesite=lax'");
        return true;
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await apiClient.LogoutAdminAsync(cancellationToken);
        }
        finally
        {
            await ClearAsync(cancellationToken);
        }
    }

    private async Task ClearAsync(CancellationToken cancellationToken)
    {
        sessionAccessor.Token = null;
        CurrentUser = null;
        await jsRuntime.InvokeVoidAsync("localStorage.removeItem", cancellationToken, StorageKey);
        await jsRuntime.InvokeVoidAsync("eval", cancellationToken, $"document.cookie = '{CookieName}=; path=/; max-age=0; samesite=lax'");
    }
}
