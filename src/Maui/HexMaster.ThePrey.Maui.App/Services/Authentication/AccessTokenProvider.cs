using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.Services.Authentication;

/// <summary>
/// Default <see cref="IAccessTokenProvider"/>. The single owner of the refresh-token exchange: reads the
/// refresh token via <see cref="ITokenStore"/>, exchanges it via <see cref="IAuth0TokenClient.RefreshAsync"/>
/// (persisting a rotated refresh token), and caches the resulting access token in memory. Registered as a
/// singleton so the cache — and the single-flight <see cref="_gate"/> around the rotating, single-use refresh
/// token — are shared across every signed-in screen and by <c>SessionService</c>. Consolidating all refresh
/// exchanges here prevents two callers from racing on the same refresh token (which, under Auth0 rotation +
/// reuse detection, revokes the whole token family and forces a re-login). Never throws — a failed exchange
/// returns <c>null</c>.
/// </summary>
public sealed class AccessTokenProvider : IAccessTokenProvider
{
    private readonly ITokenStore _tokenStore;
    private readonly IAuth0TokenClient _auth0;
    private readonly ILogger<AccessTokenProvider> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _cachedAccessToken;

    public AccessTokenProvider(ITokenStore tokenStore, IAuth0TokenClient auth0, ILogger<AccessTokenProvider> logger)
    {
        _tokenStore = tokenStore;
        _auth0 = auth0;
        _logger = logger;
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        if (_cachedAccessToken is not null)
            return _cachedAccessToken;

        await _gate.WaitAsync(ct);
        try
        {
            // Re-check inside the gate: another caller may have populated the cache while we waited.
            if (_cachedAccessToken is not null)
                return _cachedAccessToken;

            var refreshToken = await _tokenStore.GetRefreshTokenAsync();
            if (string.IsNullOrWhiteSpace(refreshToken))
                return null;

            var result = await _auth0.RefreshAsync(refreshToken, ct);
            switch (result.Outcome)
            {
                case Auth0TokenOutcome.Rejected:
                    // The refresh token is dead — clear it so we don't retry a doomed exchange.
                    _tokenStore.ClearRefreshToken();
                    return null;

                case Auth0TokenOutcome.TransientFailure:
                    // Could not reach Auth0. Keep the token; the caller degrades to an error state.
                    return null;
            }

            // Persist a rotated refresh token if Auth0 issued a new one.
            if (!string.IsNullOrWhiteSpace(result.RefreshToken) &&
                !string.Equals(result.RefreshToken, refreshToken, StringComparison.Ordinal))
            {
                await _tokenStore.SetRefreshTokenAsync(result.RefreshToken!);
            }

            _cachedAccessToken = result.AccessToken;
            return _cachedAccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to acquire an access token.");
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void SetAccessToken(string accessToken)
    {
        if (!string.IsNullOrWhiteSpace(accessToken))
            _cachedAccessToken = accessToken;
    }

    public void Invalidate() => _cachedAccessToken = null;
}
