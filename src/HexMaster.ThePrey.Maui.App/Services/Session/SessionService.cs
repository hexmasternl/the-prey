using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;

namespace HexMaster.ThePrey.Maui.App.Services.Session;

/// <summary>
/// Default <see cref="ISessionService"/>. Composes <see cref="ITokenStore"/>,
/// <see cref="IAuth0TokenClient"/> and <see cref="IGameApiClient"/>. This is the primary
/// unit-tested class — every branch below is a spec scenario.
/// </summary>
public sealed class SessionService : ISessionService
{
    private readonly ITokenStore _tokenStore;
    private readonly IAuth0TokenClient _auth0;
    private readonly IGameApiClient _gameApi;

    public SessionService(ITokenStore tokenStore, IAuth0TokenClient auth0, IGameApiClient gameApi)
    {
        _tokenStore = tokenStore;
        _auth0 = auth0;
        _gameApi = gameApi;
    }

    public async Task<SessionResult> TryEstablishSessionAsync(CancellationToken ct = default)
    {
        var refreshToken = await _tokenStore.GetRefreshTokenAsync();
        if (string.IsNullOrWhiteSpace(refreshToken))
            return SessionResult.Unauthenticated;

        var tokenResult = await _auth0.RefreshAsync(refreshToken, ct);
        switch (tokenResult.Outcome)
        {
            case Auth0TokenOutcome.Rejected:
                // Auth0 says the refresh token is dead — clear it so we don't retry a doomed exchange.
                _tokenStore.ClearRefreshToken();
                return SessionResult.Unauthenticated;

            case Auth0TokenOutcome.TransientFailure:
                // Could not reach Auth0. Keep the token; just fall back to login for now.
                return SessionResult.Unauthenticated;
        }

        // Persist a rotated refresh token if Auth0 issued a new one.
        if (!string.IsNullOrWhiteSpace(tokenResult.RefreshToken) &&
            !string.Equals(tokenResult.RefreshToken, refreshToken, StringComparison.Ordinal))
        {
            await _tokenStore.SetRefreshTokenAsync(tokenResult.RefreshToken!);
        }

        var gameResult = await _gameApi.GetActiveGameAsync(tokenResult.AccessToken!, ct);
        return gameResult.Outcome switch
        {
            ActiveGameOutcome.HasActiveGame => SessionResult.Active(gameResult.Game!),
            ActiveGameOutcome.NoActiveGame => SessionResult.NoGame,
            // Backend rejected the freshly-minted access token, or the call errored: we cannot
            // confirm a session, so route the user to login rather than into a broken game view.
            _ => SessionResult.Unauthenticated
        };
    }
}
