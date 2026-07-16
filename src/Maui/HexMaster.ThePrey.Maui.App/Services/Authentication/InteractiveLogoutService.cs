using HexMaster.ThePrey.Maui.App.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HexMaster.ThePrey.Maui.App.Services.Authentication;

/// <summary>
/// Default <see cref="IInteractiveLogoutService"/>. Clears the stored refresh token first so the
/// app is signed out regardless of what happens next, then opens the Auth0 <c>/v2/logout</c>
/// endpoint in the system web authenticator to end the tenant SSO session. Auth0 redirects to the
/// registered callback URL, which the web authenticator captures as completion (and the Android
/// callback activity uses to dismiss the browser). Browser failures are swallowed — the local
/// sign-out has already happened.
/// </summary>
public sealed class InteractiveLogoutService : IInteractiveLogoutService
{
    private readonly ITokenStore _tokenStore;
    private readonly IWebAuthenticator _webAuthenticator;
    private readonly ThePreyClientOptions _options;
    private readonly ILogger<InteractiveLogoutService> _logger;

    public InteractiveLogoutService(
        ITokenStore tokenStore,
        IWebAuthenticator webAuthenticator,
        IOptions<ThePreyClientOptions> options,
        ILogger<InteractiveLogoutService> logger)
    {
        _tokenStore = tokenStore;
        _webAuthenticator = webAuthenticator;
        _options = options.Value;
        _logger = logger;
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        // Clear the local session first: whatever the browser round-trip does, the app is signed out.
        _tokenStore.ClearRefreshToken();

        try
        {
            await _webAuthenticator.AuthenticateAsync(new WebAuthenticatorOptions
            {
                Url = Auth0LogoutUrl.Build(_options),
                CallbackUrl = new Uri(_options.RedirectUri)
            });
        }
        catch (TaskCanceledException)
        {
            // User dismissed the logout browser before it returned. The SSO session may still be
            // ended by Auth0, and the local session is already cleared — not an error.
            _logger.LogInformation("Auth0 sign-out browser was dismissed before completion.");
        }
        catch (Exception ex)
        {
            // Could not complete the federated logout (e.g. no network). The local session is already
            // cleared, so the app is signed out; the tenant SSO cookie may simply persist.
            _logger.LogWarning(ex, "Auth0 federated sign-out did not complete; local session was cleared.");
        }
    }
}
