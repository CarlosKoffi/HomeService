using HomeService.Contracts.Notifications;
#if ANDROID
using Android.Gms.Extensions;
#endif

namespace HomeService.Company.Mobile.Services;

public sealed class CompanyDeviceRegistrationService(
    CompanyMobileApiClient apiClient,
    CompanySessionStore sessionStore)
{
    public async Task RegisterCurrentDeviceAsync(CancellationToken cancellationToken = default)
    {
        var token = await sessionStore.GetTokenAsync();
        var companyId = await sessionStore.GetCompanyIdAsync();
        if (string.IsNullOrWhiteSpace(token) || !companyId.HasValue)
        {
            return;
        }

        var firebaseToken = await ResolveFirebaseTokenAsync();
        if (string.IsNullOrWhiteSpace(firebaseToken))
        {
            return;
        }

        await apiClient.RegisterDeviceTokenAsync(
            token,
            companyId.Value,
            new RegisterMobileDeviceTokenRequest(
                firebaseToken,
                DeviceInfo.Current.Platform.ToString(),
                $"{DeviceInfo.Current.Manufacturer} {DeviceInfo.Current.Model}".Trim()),
            cancellationToken);
    }

    private static Task<string> ResolveFirebaseTokenAsync()
    {
#if ANDROID
        return ResolveAndroidFirebaseTokenAsync();
#else
        return Task.FromResult(string.Empty);
#endif
    }

#if ANDROID
    private static async Task<string> ResolveAndroidFirebaseTokenAsync()
    {
        try
        {
            var javaToken = await Firebase.Messaging.FirebaseMessaging.Instance.GetToken().AsAsync<Java.Lang.String>();
            return javaToken?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
#endif
}
