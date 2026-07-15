using Foundation;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using Microsoft.Extensions.DependencyInjection;
using UIKit;

namespace HexMaster.ThePrey.Maui.App
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        // Apple's activity type for Universal Links (NSUserActivityTypeBrowsingWeb).
        private const string BrowsingWebActivityType = "NSUserActivityTypeBrowsingWeb";

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        // Universal Link continuation: iOS delivers the tapped https link here (cold start and running app).
        // Backed by the applinks:theprey.nl Associated Domains entitlement plus the apple-app-site-association
        // hosted on theprey.nl (a hosting task, tracked in the proposal).
        public override bool ContinueUserActivity(
            UIApplication application, NSUserActivity userActivity, UIApplicationRestorationHandler completionHandler)
        {
            if (userActivity.ActivityType == BrowsingWebActivityType
                && userActivity.WebpageUrl?.AbsoluteString is string url
                && Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                var handler = IPlatformApplication.Current?.Services.GetService<IInviteDeepLinkHandler>();

                // Queue then replay: on a cold-start continuation the Shell may not be ready yet (the window
                // Activated hook replays it); on a running app the queued link is routed here immediately.
                handler?.QueuePending(uri);
                _ = handler?.ReplayPendingAsync();
                return true;
            }

            return base.ContinueUserActivity(application, userActivity, completionHandler);
        }
    }
}
