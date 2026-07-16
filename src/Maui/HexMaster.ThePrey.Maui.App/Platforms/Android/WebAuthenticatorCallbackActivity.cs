using Android.App;
using Android.Content;
using Android.Content.PM;

namespace HexMaster.ThePrey.Maui.App
{
    // Receives the Auth0 redirect (theprey://callback) and hands it back to WebAuthenticator.
    [Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = CallbackScheme,
        DataHost = CallbackHost)]
    public class WebAuthenticatorCallbackActivity : Microsoft.Maui.Authentication.WebAuthenticatorCallbackActivity
    {
        private const string CallbackScheme = "theprey";
        private const string CallbackHost = "callback";

        // Once WebAuthenticator has captured the redirect, the Chrome Custom Tab that hosted the
        // Auth0 page is still on the task stack and stays on top of the app. Reorder MainActivity to
        // the front (SingleTop reuses the existing instance, so no fresh bootstrap) which pops the
        // Custom Tab off, returning the user to the app with no leftover browser. Applies to both the
        // login and the logout round-trips.
        protected override void OnResume()
        {
            base.OnResume();

            var intent = new Intent(this, typeof(MainActivity));
            intent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
            StartActivity(intent);
        }
    }
}
