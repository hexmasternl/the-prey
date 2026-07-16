using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using HexMaster.ThePrey.Maui.App.Services.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace HexMaster.ThePrey.Maui.App
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    // Verified App Link for the invite: https://theprey.nl/join/*. android:autoVerify is backed by the
    // .well-known/assetlinks.json hosted on theprey.nl (a hosting task, tracked in the proposal). Until it is
    // published the link opens the browser instead of the app; the flow still degrades gracefully.
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "https",
        DataHost = "theprey.nl",
        DataPathPrefix = "/join",
        AutoVerify = true)]
    public class MainActivity : MauiAppCompatActivity
    {
        private static IInviteDeepLinkHandler? DeepLinkHandler =>
            IPlatformApplication.Current?.Services.GetService<IInviteDeepLinkHandler>();

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Cold start: the launch intent may carry the invite link. Queue it — the Shell is not ready yet;
            // App.CreateWindow replays it once the window is up.
            if (TryGetInviteLink(Intent, out var uri))
                DeepLinkHandler?.QueuePending(uri);
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);

            // Running app (SingleTop): route immediately.
            if (TryGetInviteLink(intent, out var uri))
                _ = DeepLinkHandler?.TryHandleAsync(uri);
        }

        private static bool TryGetInviteLink(Intent? intent, out Uri? uri)
        {
            uri = null;
            if (intent is null || intent.Action != Intent.ActionView)
                return false;

            var data = intent.DataString;
            if (string.IsNullOrEmpty(data) || !Uri.TryCreate(data, UriKind.Absolute, out var parsed))
                return false;

            uri = parsed;
            return true;
        }
    }
}
