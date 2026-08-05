using HomeService.Contracts.Notifications;
#if ANDROID
using Android.Gms.Extensions;
#endif

namespace HomeService.Provider.Mobile.Services;

public sealed class ProviderDeviceRegistrationService(
    ProviderMobileApiClient apiClient,
    ProviderSessionService sessionService)
{
    private const string FirebaseTokenKey = "ProviderFirebaseCloudMessagingToken";

    public async Task RegisterCurrentDeviceAsync(CancellationToken cancellationToken = default)
    {
        var accessToken = await sessionService.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        var firebaseToken = await ResolveFirebaseTokenAsync();
        if (string.IsNullOrWhiteSpace(firebaseToken))
        {
            return;
        }

        var request = new RegisterMobileDeviceTokenRequest(
            firebaseToken.Trim(),
            DeviceInfo.Current.Platform.ToString(),
            BuildDeviceLabel());
        var result = await apiClient.RegisterDeviceTokenAsync(accessToken, request, cancellationToken);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                result.ErrorMessage ?? "Le téléphone n'a pas pu être enregistré pour les notifications.");
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
            // Retried on the next authenticated app start/resume.
        }
#endif
        return Preferences.Default.Get(FirebaseTokenKey, string.Empty);
    }

    private static string BuildDeviceLabel()
    {
        return string.Join(
            " - ",
            new[] { DeviceInfo.Current.Manufacturer, DeviceInfo.Current.Model, DeviceInfo.Current.Name }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
    }
}
