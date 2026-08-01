using HomeService.Contracts.Notifications;
#if ANDROID
using Android.Gms.Extensions;
#endif

namespace HomeService.Client.Mobile.Services;

public sealed class ClientDeviceRegistrationService(ClientMobileApiClient apiClient)
{
    private const string FirebaseTokenKey = "FirebaseCloudMessagingToken";
    private const string LastRegisteredTokenKey = "LastRegisteredFirebaseCloudMessagingToken";

    public async Task RegisterCurrentDeviceAsync(CancellationToken cancellationToken = default)
    {
        var token = await ResolveFirebaseTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var lastRegisteredToken = Preferences.Default.Get(LastRegisteredTokenKey, string.Empty);
        if (string.Equals(lastRegisteredToken, token, StringComparison.Ordinal))
        {
            return;
        }

        var request = new RegisterMobileDeviceTokenRequest(
            token.Trim(),
            DeviceInfo.Current.Platform.ToString(),
            BuildDeviceLabel());

        var result = await apiClient.RegisterDeviceTokenAsync(request, cancellationToken);
        if (result.IsSuccess)
        {
            Preferences.Default.Set(LastRegisteredTokenKey, token);
        }
    }

    public static void StoreFirebaseToken(string token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            Preferences.Default.Set(FirebaseTokenKey, token.Trim());
        }
    }

    private static async Task<string> ResolveFirebaseTokenAsync()
    {
#if ANDROID
        try
        {
            var javaToken = await Firebase.Messaging.FirebaseMessaging.Instance
                .GetToken()
                .AsAsync<Java.Lang.String>();
            var token = javaToken?.ToString() ?? string.Empty;
            StoreFirebaseToken(token);
            return token;
        }
        catch
        {
            // Registration is retried on the next authenticated app start.
        }
#endif
        return Preferences.Default.Get(FirebaseTokenKey, string.Empty);
    }

    private static string BuildDeviceLabel()
    {
        var manufacturer = DeviceInfo.Current.Manufacturer;
        var model = DeviceInfo.Current.Model;
        var name = DeviceInfo.Current.Name;

        return string.Join(" - ", new[] { manufacturer, model, name }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }
}
