using Foundation;
using HomeService.Provider.Mobile.Services;
using UIKit;

namespace HomeService.Provider.Mobile;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp()
    {
        return MauiProgram.CreateMauiApp();
    }

    public override bool OpenUrl(UIApplication application, NSUrl url, NSDictionary options)
    {
        if (Uri.TryCreate(url.AbsoluteString, UriKind.Absolute, out var uri))
        {
            ProviderDeepLinkNavigationService.Store(uri);
            _ = ProviderDeepLinkNavigationService.TryNavigateAsync();
            return true;
        }

        return base.OpenUrl(application, url, options);
    }
}
