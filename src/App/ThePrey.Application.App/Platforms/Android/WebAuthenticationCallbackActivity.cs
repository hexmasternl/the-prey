using Android.App;
using Android.Content;
using Android.Content.PM;

namespace ThePrey.Application.App;

/// <summary>
/// Handles the Auth0 redirect URI callback on Android.
/// The DataScheme must match the application ID (used as the redirect URI scheme).
/// </summary>
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "com.hexmaster.theprey.application.app",
    DataHost = "callback")]
public class WebAuthenticationCallbackActivity
    : Microsoft.Maui.Authentication.WebAuthenticatorCallbackActivity
{
}
