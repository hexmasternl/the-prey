using Auth0.OidcClient;
using System.Text;
using System.Text.Json;

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

    /// <summary>
    /// Returns a valid access token, transparently refreshing it when it is expired or within
    /// 30 seconds of expiry. Returns <c>null</c> and clears the session when refresh fails.
    /// All HTTP service classes MUST use this method instead of reading <see cref="AccessToken"/> directly.
    /// </summary>
    Task<string?> GetAccessTokenAsync(CancellationToken ct = default);

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

    // Serializes concurrent GetAccessTokenAsync calls so only one refresh is in-flight at a time.
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    // Seconds before expiry at which we proactively refresh (clock-skew buffer).
    private const int ExpiryBufferSeconds = 30;

    public bool IsAuthenticated { get; private set; }
    public string? AccessToken { get; private set; }

    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        if (!IsAuthenticated)
            return null;

        if (!IsTokenExpiredOrExpiringSoon(AccessToken))
            return AccessToken;

        await _refreshLock.WaitAsync(ct);
        try
        {
            // Re-check under the lock: another caller may have refreshed while we waited.
            if (!IsTokenExpiredOrExpiringSoon(AccessToken))
                return AccessToken;

            var refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                Clear();
                return null;
            }

            var result = await client.RefreshTokenAsync(refreshToken);
            if (result.IsError)
            {
                Clear();
                return null;
            }

            await StoreSessionAsync(result.AccessToken, result.RefreshToken);
            return AccessToken;
        }
        catch
        {
            Clear();
            return null;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

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

    /// <summary>
    /// Returns true when the JWT is missing, malformed, already expired, or expiring within
    /// <see cref="ExpiryBufferSeconds"/> seconds.
    /// </summary>
    private static bool IsTokenExpiredOrExpiringSoon(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return true;

        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
                return true;

            // Base64url → standard Base64 → bytes → JSON
            var payload = parts[1].Replace("-", "+").Replace("_", "/");
            var remainder = payload.Length % 4;
            if (remainder == 2)
                payload += "==";
            else if (remainder == 3)
                payload += "=";

            var bytes = Convert.FromBase64String(payload);
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(bytes));

            if (!doc.RootElement.TryGetProperty("exp", out var expElement))
                return true;

            var exp = DateTimeOffset.FromUnixTimeSeconds(expElement.GetInt64());
            return DateTimeOffset.UtcNow >= exp.AddSeconds(-ExpiryBufferSeconds);
        }
        catch
        {
            return true;
        }
    }
}
