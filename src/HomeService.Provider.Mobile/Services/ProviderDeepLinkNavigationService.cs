using HomeService.Provider.Mobile.Pages;

namespace HomeService.Provider.Mobile.Services;

public static class ProviderDeepLinkNavigationService
{
    private static readonly object SyncRoot = new();
    private static ProviderDeepLinkRequest? pendingRequest;

    public static void Store(Uri? uri)
    {
        var request = Parse(uri);
        if (request is null)
        {
            return;
        }

        lock (SyncRoot)
        {
            pendingRequest = request;
        }
    }

    public static Page CreateInitialPage()
    {
        return CreatePage(TakePendingRequest());
    }

    public static async Task<bool> TryNavigateAsync()
    {
        var request = TakePendingRequest();
        if (request is null || Application.Current?.Windows.FirstOrDefault() is not { } window)
        {
            return false;
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            window.Page = Wrap(CreatePage(request));
        });
        return true;
    }

    public static NavigationPage Wrap(Page page)
    {
        return new NavigationPage(page)
        {
            BarBackgroundColor = Colors.White,
            BarTextColor = Color.FromArgb("#0F172A")
        };
    }

    private static ProviderDeepLinkRequest? TakePendingRequest()
    {
        lock (SyncRoot)
        {
            var request = pendingRequest;
            pendingRequest = null;
            return request;
        }
    }

    private static Page CreatePage(ProviderDeepLinkRequest? request)
    {
        return request?.Kind switch
        {
            ProviderDeepLinkKind.Activation when !string.IsNullOrWhiteSpace(request.InvitationCode)
                => new ProviderActivationPage(request.InvitationCode),
            ProviderDeepLinkKind.Login => new LoginPage(request.PhoneNumber),
            _ => new LoginPage()
        };
    }

    private static ProviderDeepLinkRequest? Parse(Uri? uri)
    {
        if (uri is null)
        {
            return null;
        }

        var destination = uri.Scheme.Equals("wele-provider", StringComparison.OrdinalIgnoreCase)
            ? uri.Host
            : uri.AbsolutePath.Trim('/');

        if (destination.Equals("activation", StringComparison.OrdinalIgnoreCase))
        {
            var code = GetQueryValue(uri, "code");
            return string.IsNullOrWhiteSpace(code)
                ? null
                : new ProviderDeepLinkRequest(ProviderDeepLinkKind.Activation, code.Trim(), null);
        }

        if (destination.Equals("login", StringComparison.OrdinalIgnoreCase))
        {
            return new ProviderDeepLinkRequest(
                ProviderDeepLinkKind.Login,
                null,
                GetQueryValue(uri, "phone")?.Trim());
        }

        return null;
    }

    private static string? GetQueryValue(Uri uri, string name)
    {
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (!Uri.UnescapeDataString(parts[0]).Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return parts.Length == 2
                ? Uri.UnescapeDataString(parts[1].Replace('+', ' '))
                : string.Empty;
        }

        return null;
    }
}

public enum ProviderDeepLinkKind
{
    Activation,
    Login
}

public sealed record ProviderDeepLinkRequest(
    ProviderDeepLinkKind Kind,
    string? InvitationCode,
    string? PhoneNumber);
