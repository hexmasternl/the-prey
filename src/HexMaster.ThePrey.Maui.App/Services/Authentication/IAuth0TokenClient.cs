namespace HexMaster.ThePrey.Maui.App.Services.Authentication;

/// <summary>Outcome of an Auth0 token-endpoint exchange.</summary>
public enum Auth0TokenOutcome
{
    /// <summary>An access token was issued.</summary>
    Success,

    /// <summary>Auth0 explicitly rejected the grant (e.g. <c>invalid_grant</c>): the refresh token is dead.</summary>
    Rejected,

    /// <summary>The exchange could not be completed (network error, timeout, 5xx). The token may still be valid.</summary>
    TransientFailure
}

/// <summary>Result of an Auth0 token exchange.</summary>
public sealed record Auth0TokenResult(Auth0TokenOutcome Outcome, string? AccessToken, string? RefreshToken)
{
    public static Auth0TokenResult FromSuccess(string accessToken, string? refreshToken) =>
        new(Auth0TokenOutcome.Success, accessToken, refreshToken);

    public static readonly Auth0TokenResult Rejected = new(Auth0TokenOutcome.Rejected, null, null);

    public static readonly Auth0TokenResult Transient = new(Auth0TokenOutcome.TransientFailure, null, null);
}

/// <summary>Talks to the Auth0 <c>/oauth/token</c> endpoint.</summary>
public interface IAuth0TokenClient
{
    /// <summary>Exchanges a refresh token for a fresh access token (and possibly a rotated refresh token).</summary>
    Task<Auth0TokenResult> RefreshAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Exchanges a PKCE authorization code for tokens (interactive login).</summary>
    Task<Auth0TokenResult> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct = default);
}
