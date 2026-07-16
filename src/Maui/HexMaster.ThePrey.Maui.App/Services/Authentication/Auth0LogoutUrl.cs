using System.Web;
using HexMaster.ThePrey.Maui.App.Configuration;

namespace HexMaster.ThePrey.Maui.App.Services.Authentication;

/// <summary>
/// Builds the Auth0 <c>/v2/logout</c> URL used to end the tenant SSO session on sign-out. Pure and
/// MAUI-free so it is unit-testable in isolation; the browser round-trip lives in
/// <see cref="InteractiveLogoutService"/>. The <c>returnTo</c> value must be registered as an Allowed
/// Logout URL in the Auth0 application.
/// </summary>
public static class Auth0LogoutUrl
{
    public static Uri Build(ThePreyClientOptions options)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = options.Auth0ClientId;
        query["returnTo"] = options.RedirectUri;

        return new UriBuilder(options.LogoutEndpoint) { Query = query.ToString() }.Uri;
    }
}
