using Auth0.OidcClient;

namespace ThePrey.Application.App.Services;

/// <summary>
/// Owns the app's authentication state and the "remembered" login session.
/// A refresh token is persisted in <see cref="SecureStorage"/> after login and used to
/// silently restore the session on a later launch.
/// </summary>
public interface IAuthService
{
    bool IsAuthenticated { get; }
    string? AccessToken { get; }

    /// <summary>Attempts to restore a previously remembered session using the stored refresh token.</summary>
    Task<bool> RestoreSessionAsync();

    /// <summary>Runs the interactive Auth0 login (optionally the sign-up screen).</summary>
    Task<bool> LoginAsync(bool signUp = false);

    /// <summary>Clears the session and the remembered refresh token.</summary>
    Task LogoutAsync();
}

public sealed class AuthService(Auth0Client client) : IAuthService
{
    private const string RefreshTokenKey = "auth0_refresh_token";

    public bool IsAuthenticated { get; private set; }
    public string? AccessToken { get; private set; }

    public async Task<bool> RestoreSessionAsync()
    {
        try
        {
            var refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
            if (string.IsNullOrWhiteSpace(refreshToken))
                return false;

            var result = await client.RefreshTokenAsync(refreshToken);
            if (result.IsError)
            {
                Clear();
                return false;
            }

            await StoreSessionAsync(result.AccessToken, result.RefreshToken);
            return true;
        }
        catch
        {
            // A failed/expired refresh must never block startup — fall back to interactive login.
            Clear();
            return false;
        }
    }

    public async Task<bool> LoginAsync(bool signUp = false)
    {
        // Auth0Client.LoginAsync reflects over the extra-parameters object, so pass an anonymous
        // object (not a dictionary). offline_access (configured on the client) yields the refresh token.
        var result = signUp
            ? await client.LoginAsync(new { screen_hint = "signup", audience = MauiProgram.Auth0Audience })
            : await client.LoginAsync(new { audience = MauiProgram.Auth0Audience });

        if (result.IsError)
            return false;

        await StoreSessionAsync(result.AccessToken, result.RefreshToken);
        return true;
    }

    public async Task LogoutAsync()
    {
        try
        {
            await client.LogoutAsync();
        }
        catch
        {
            // Best-effort: clear local state even if the remote logout call fails.
        }

        Clear();
        SecureStorage.Default.Remove(RefreshTokenKey);
    }

    private async Task StoreSessionAsync(string? accessToken, string? refreshToken)
    {
        AccessToken = accessToken;
        IsAuthenticated = true;

        if (string.IsNullOrWhiteSpace(refreshToken))
            return;

        try
        {
            // Refresh tokens may be rotated on each use; always persist the latest one.
            await SecureStorage.Default.SetAsync(RefreshTokenKey, refreshToken);
        }
        catch
        {
            // Persisting is best-effort: the session is valid for this run even if the
            // platform secure store is unavailable; it just won't be remembered next launch.
        }
    }

    private void Clear()
    {
        IsAuthenticated = false;
        AccessToken = null;
    }
}
