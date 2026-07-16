using System.Web;
using HexMaster.ThePrey.Maui.App.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HexMaster.ThePrey.Maui.App.Services.Authentication;

/// <summary>
/// Default <see cref="IInteractiveLoginService"/>. Builds the Auth0 <c>/authorize</c> URL with a
/// PKCE challenge, opens it in the system web authenticator, exchanges the returned code for
/// tokens, and stores the refresh token. Extracted from the login view model so the main menu
/// can invoke the same flow.
/// </summary>
public sealed class InteractiveLoginService : IInteractiveLoginService
{
    private readonly IAuth0TokenClient _auth0;
    private readonly ITokenStore _tokenStore;
    private readonly IWebAuthenticator _webAuthenticator;
    private readonly ThePreyClientOptions _options;
    private readonly ILogger<InteractiveLoginService> _logger;

    public InteractiveLoginService(
        IAuth0TokenClient auth0,
        ITokenStore tokenStore,
        IWebAuthenticator webAuthenticator,
        IOptions<ThePreyClientOptions> options,
        ILogger<InteractiveLoginService> logger)
    {
        _auth0 = auth0;
        _tokenStore = tokenStore;
        _webAuthenticator = webAuthenticator;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<InteractiveLoginOutcome> LoginAsync(CancellationToken ct = default)
    {
        try
        {
            var pkce = PkcePair.Create();
            var state = PkcePair.CreateState();
            var authorizeUrl = BuildAuthorizeUrl(pkce.Challenge, state);

            var authResult = await _webAuthenticator.AuthenticateAsync(new WebAuthenticatorOptions
            {
                Url = authorizeUrl,
                CallbackUrl = new Uri(_options.RedirectUri)
            });

            if (authResult.Properties.TryGetValue("state", out var returnedState) &&
                !string.Equals(returnedState, state, StringComparison.Ordinal))
            {
                _logger.LogWarning("Auth0 login returned a mismatched state value.");
                return InteractiveLoginOutcome.Failed;
            }

            if (!authResult.Properties.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            {
                _logger.LogWarning("Auth0 login did not return an authorization code.");
                return InteractiveLoginOutcome.Failed;
            }

            var tokenResult = await _auth0.ExchangeCodeAsync(code, pkce.Verifier, ct);
            if (tokenResult.Outcome != Auth0TokenOutcome.Success)
            {
                _logger.LogWarning("Auth0 code exchange failed with outcome {Outcome}.", tokenResult.Outcome);
                return InteractiveLoginOutcome.Failed;
            }

            if (string.IsNullOrWhiteSpace(tokenResult.RefreshToken))
            {
                // The code exchange succeeded but Auth0 issued no refresh token. Without one the app
                // cannot stay signed in across launches. This is a configuration problem, not a user error.
                _logger.LogError(
                    "Interactive login succeeded but no refresh token was issued. Enable 'Allow Offline Access' " +
                    "on the Auth0 API and ensure the app requests the offline_access scope.");
                return InteractiveLoginOutcome.NoRefreshToken;
            }

            await _tokenStore.SetRefreshTokenAsync(tokenResult.RefreshToken!);
            return InteractiveLoginOutcome.Success;
        }
        catch (TaskCanceledException)
        {
            // User dismissed the system browser — not an error.
            _logger.LogInformation("Interactive login was cancelled by the user.");
            return InteractiveLoginOutcome.Cancelled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Interactive login failed.");
            return InteractiveLoginOutcome.Failed;
        }
    }

    private Uri BuildAuthorizeUrl(string codeChallenge, string state)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["response_type"] = "code";
        query["client_id"] = _options.Auth0ClientId;
        query["redirect_uri"] = _options.RedirectUri;
        query["scope"] = "openid profile offline_access";
        query["audience"] = _options.Audience;
        query["code_challenge"] = codeChallenge;
        query["code_challenge_method"] = PkcePair.ChallengeMethod;
        query["state"] = state;

        return new UriBuilder(_options.AuthorizeEndpoint) { Query = query.ToString() }.Uri;
    }
}
