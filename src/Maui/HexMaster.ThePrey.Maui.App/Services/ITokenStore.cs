namespace HexMaster.ThePrey.Maui.App.Services;

/// <summary>
/// Persists the long-lived Auth0 refresh token in the platform secure store.
/// The short-lived access token is never persisted — it lives only in memory for the app session.
/// </summary>
public interface ITokenStore
{
    Task<string?> GetRefreshTokenAsync();

    Task SetRefreshTokenAsync(string refreshToken);

    void ClearRefreshToken();
}
