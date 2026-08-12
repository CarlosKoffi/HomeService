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
    public string? LastSignInError { get; private set; }

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
            else
            {
                await SetSessionCookieAsync(token, cancellationToken);
            }
        }
        catch
        {
            await ClearAsync(cancellationToken);
        }
    }

    public async Task<bool> SignInAsync(
        string email,
        string password,
        string? mfaCode = null,
        CancellationToken cancellationToken = default)
    {
        LastSignInError = null;
        AdminLoginResponse? response;
        try
        {
            response = await apiClient.LoginAdminAsync(new AdminLoginRequest(email, password, mfaCode), cancellationToken);
        }
        catch (PlatformApiException exception)
        {
            LastSignInError = exception.Message;
            return false;
        }
        catch (HttpRequestException)
        {
            LastSignInError = "Le service de connexion est temporairement indisponible. Reessayez dans un instant.";
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LastSignInError = "Le service de connexion met trop de temps a repondre. Reessayez.";
            return false;
        }
        catch (System.Text.Json.JsonException)
        {
            LastSignInError = "La reponse du service de connexion est invalide. Actualisez la page puis reessayez.";
            return false;
        }

        if (response is null)
        {
            LastSignInError = "Connexion refusée.";
            return false;
        }

        sessionAccessor.Token = response.Token;
        CurrentUser = response.User;
        try
        {
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", cancellationToken, StorageKey, response.Token);
            await SetSessionCookieAsync(response.Token, cancellationToken);
        }
        catch (Exception exception) when (IsBrowserSessionException(exception))
        {
            sessionAccessor.Token = null;
            CurrentUser = null;
            LastSignInError = "Votre navigateur a bloque l'enregistrement de la session. Actualisez la page puis reessayez.";
            return false;
        }

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

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionAccessor.Token))
        {
            CurrentUser = null;
            return;
        }

        CurrentUser = await apiClient.GetCurrentAdminAsync(cancellationToken);
    }

    private async Task ClearAsync(CancellationToken cancellationToken)
    {
        sessionAccessor.Token = null;
        CurrentUser = null;
        await jsRuntime.InvokeVoidAsync("localStorage.removeItem", cancellationToken, StorageKey);
        await jsRuntime.InvokeVoidAsync("weleAdminSession.clearCookie", cancellationToken, CookieName);
    }

    private async Task SetSessionCookieAsync(string token, CancellationToken cancellationToken)
    {
        await jsRuntime.InvokeVoidAsync(
            "weleAdminSession.setCookie",
            cancellationToken,
            CookieName,
            token,
            28800);
    }

    private static bool IsBrowserSessionException(Exception exception) =>
        exception is JSException
        || exception.GetType().Name == "JSDisconnectedException"
        || exception is InvalidOperationException;
}
