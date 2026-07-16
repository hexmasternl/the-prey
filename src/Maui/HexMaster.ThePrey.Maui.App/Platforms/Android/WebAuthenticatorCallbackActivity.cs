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
    }
}
