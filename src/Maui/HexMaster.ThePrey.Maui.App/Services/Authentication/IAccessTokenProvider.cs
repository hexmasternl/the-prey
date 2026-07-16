namespace HexMaster.ThePrey.Maui.App.Services.Authentication;

/// <summary>
/// Supplies a bearer access token for authenticated backend calls. Reads the stored refresh token
/// and exchanges it via Auth0, caching the access token in memory for reuse across calls. The
/// reusable authenticated-call seam shared by the app's signed-in screens.
/// </summary>
public interface IAccessTokenProvider
{
    /// <summary>
    /// Returns a valid access token, exchanging the stored refresh token if none is cached, or
    /// <c>null</c> when there is no refresh token or the exchange fails (rejected/transient). Never throws.
    /// </summary>
    Task<string?> GetAccessTokenAsync(CancellationToken ct = default);

    /// <summary>Drops the cached access token so the next request re-exchanges (call after a 401).</summary>
    void Invalidate();
}
