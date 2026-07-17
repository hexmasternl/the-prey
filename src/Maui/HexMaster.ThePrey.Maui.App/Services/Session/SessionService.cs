using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;

namespace HexMaster.ThePrey.Maui.App.Services.Session;

/// <summary>
/// Default <see cref="ISessionService"/>. Resolves the access token through the shared
/// <see cref="IAccessTokenProvider"/> (the single owner of refresh-token exchange + rotation) and then checks
/// for an active game via <see cref="IGameApiClient"/>. It deliberately does NOT exchange the refresh token
/// itself: doing so used to make two uncoordinated callers spend the same single-use refresh token, which —
/// under Auth0 rotation — could revoke the token family and force a re-login. This is the primary unit-tested
/// class — every branch below is a spec scenario.
/// </summary>
public sealed class SessionService : ISessionService
{
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly IGameApiClient _gameApi;

    public SessionService(IAccessTokenProvider accessTokenProvider, IGameApiClient gameApi)
    {
        _accessTokenProvider = accessTokenProvider;
        _gameApi = gameApi;
    }

    public async Task<SessionResult> TryEstablishSessionAsync(CancellationToken ct = default)
    {
        // Delegates the refresh-token exchange (and rotation persistence, rejected/transient handling, and
        // caching) to the provider. A null token means no valid refresh token — treat as unauthenticated.
        var accessToken = await _accessTokenProvider.GetAccessTokenAsync(ct);
        if (accessToken is null)
            return SessionResult.Unauthenticated;

        var gameResult = await _gameApi.GetActiveGameAsync(accessToken, ct);
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
