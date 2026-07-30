using HomeService.Contracts.Notifications;

namespace HomeService.Client.Mobile.Services;

public sealed class ClientDeviceRegistrationService(ClientMobileApiClient apiClient)
{
    private const string FirebaseTokenKey = "FirebaseCloudMessagingToken";
    private const string LastRegisteredTokenKey = "LastRegisteredFirebaseCloudMessagingToken";

    public async Task RegisterCurrentDeviceAsync(CancellationToken cancellationToken = default)
    {
        var token = Preferences.Default.Get(FirebaseTokenKey, string.Empty);
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

    private static string BuildDeviceLabel()
    {
        var manufacturer = DeviceInfo.Current.Manufacturer;
        var model = DeviceInfo.Current.Model;
        var name = DeviceInfo.Current.Name;

        return string.Join(" - ", new[] { manufacturer, model, name }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }
}
