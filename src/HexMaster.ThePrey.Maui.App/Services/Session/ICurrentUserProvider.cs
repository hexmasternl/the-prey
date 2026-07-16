namespace HexMaster.ThePrey.Maui.App.Services.Session;

/// <summary>
/// Supplies the signed-in user's <em>internal</em> id (the backend user id, matched against a game's
/// <c>HunterUserId</c> to determine role) — distinct from the JWT <c>sub</c>. Reads it once from
/// <c>GET /users/me</c> and caches it for the session. Returns <c>null</c> when no token is available or
/// the lookup fails; never throws.
/// </summary>
public interface ICurrentUserProvider
{
    /// <summary>The caller's internal user id, or <c>null</c> when unavailable.</summary>
    Task<Guid?> GetUserIdAsync(CancellationToken ct = default);

    /// <summary>Drops the cached id so the next call re-reads it (e.g. after a sign-out/sign-in).</summary>
    void Invalidate();
}
