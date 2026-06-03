using Auth0.OidcClient;
using System.Security.Cryptography;
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

    /// <summary>Runs the interactive Auth0 login inside an embedded WebView (optionally the sign-up screen).</summary>
    Task<bool> LoginAsync(bool signUp = false);

    /// <summary>Revokes the refresh token on Auth0 and clears the local session.</summary>
    Task LogoutAsync();
}

public sealed class AuthService(Auth0Client auth0Client, IHttpClientFactory httpClientFactory) : IAuthService
{
    private const string RefreshTokenKey = "auth0_refresh_token";
    private const int ExpiryBufferSeconds = 30;

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly HttpClient _http = httpClientFactory.CreateClient();

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
            if (!IsTokenExpiredOrExpiringSoon(AccessToken))
                return AccessToken;

            var refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                Clear();
                return null;
            }

            var result = await auth0Client.RefreshTokenAsync(refreshToken);
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

            var result = await auth0Client.RefreshTokenAsync(refreshToken);
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
            Clear();
            return false;
        }
    }

    public async Task<bool> LoginAsync(bool signUp = false)
    {
        // PKCE: generate a high-entropy verifier and its SHA-256 challenge.
        var verifier = GenerateRandomBase64Url(32);
        var challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = GenerateRandomBase64Url(16);

        var authUrl = BuildAuthorizationUrl(state, challenge, signUp);
        var callbackUrl = await ShowEmbeddedBrowserAsync(authUrl);
        if (callbackUrl is null)
            return false;

        var query = ParseQueryString(callbackUrl);

        // Validate state to prevent CSRF.
        if (!query.TryGetValue("state", out var returnedState) || returnedState != state)
            return false;

        if (!query.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
            return false;

        return await ExchangeCodeAndStoreAsync(code, verifier);
    }

    public async Task LogoutAsync()
    {
        try
        {
            var refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
            if (!string.IsNullOrWhiteSpace(refreshToken))
                await RevokeTokenAsync(refreshToken);
        }
        catch
        {
            // Best-effort: clear local state even if revocation fails.
        }

        Clear();
        SecureStorage.Default.Remove(RefreshTokenKey);
    }

    // ── PKCE + OAuth helpers ─────────────────────────────────────────────────

    private static string BuildAuthorizationUrl(string state, string codeChallenge, bool signUp)
    {
        var parameters = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = MauiProgram.Auth0ClientId,
            ["redirect_uri"] = MauiProgram.RedirectUri,
            ["scope"] = "openid profile email offline_access",
            ["audience"] = MauiProgram.Auth0Audience,
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
        };
        if (signUp)
            parameters["screen_hint"] = "signup";

        var qs = string.Join("&", parameters.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        return $"https://{MauiProgram.Auth0Domain}/authorize?{qs}";
    }

    private async Task<bool> ExchangeCodeAndStoreAsync(string code, string verifier)
    {
        try
        {
            using var response = await _http.PostAsync(
                $"https://{MauiProgram.Auth0Domain}/oauth/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["client_id"] = MauiProgram.Auth0ClientId,
                    ["code_verifier"] = verifier,
                    ["code"] = code,
                    ["redirect_uri"] = MauiProgram.RedirectUri,
                }));

            if (!response.IsSuccessStatusCode)
                return false;

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var accessToken = doc.RootElement.GetProperty("access_token").GetString();
            var refreshToken = doc.RootElement.TryGetProperty("refresh_token", out var rt)
                ? rt.GetString() : null;

            if (string.IsNullOrWhiteSpace(accessToken))
                return false;

            await StoreSessionAsync(accessToken, refreshToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task RevokeTokenAsync(string refreshToken)
    {
        await _http.PostAsync(
            $"https://{MauiProgram.Auth0Domain}/oauth/revoke",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = MauiProgram.Auth0ClientId,
                ["token"] = refreshToken,
            }));
    }

    private static Task<string?> ShowEmbeddedBrowserAsync(string startUrl)
    {
        var tcs = new TaskCompletionSource<string?>();
        var page = new AuthWebViewPage(startUrl, MauiProgram.RedirectUri, tcs);
        MainThread.BeginInvokeOnMainThread(() =>
            _ = Shell.Current.Navigation.PushModalAsync(new NavigationPage(page), animated: true));
        return tcs.Task;
    }

    private static Dictionary<string, string> ParseQueryString(string url)
    {
        var idx = url.IndexOf('?');
        if (idx < 0)
            return [];

        return url[(idx + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(
                p => Uri.UnescapeDataString(p[0]),
                p => Uri.UnescapeDataString(p[1]));
    }

    private static string GenerateRandomBase64Url(int byteCount)
    {
        var bytes = new byte[byteCount];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // ── Session management ───────────────────────────────────────────────────

    private async Task StoreSessionAsync(string? accessToken, string? refreshToken)
    {
        AccessToken = accessToken;
        IsAuthenticated = true;

        if (string.IsNullOrWhiteSpace(refreshToken))
            return;

        try
        {
            await SecureStorage.Default.SetAsync(RefreshTokenKey, refreshToken);
        }
        catch
        {
            // Persisting is best-effort; the session is still valid for this run.
        }
    }

    private void Clear()
    {
        IsAuthenticated = false;
        AccessToken = null;
    }

    private static bool IsTokenExpiredOrExpiringSoon(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return true;

        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
                return true;

            var payload = parts[1].Replace("-", "+").Replace("_", "/");
            var remainder = payload.Length % 4;
            if (remainder == 2) payload += "==";
            else if (remainder == 3) payload += "=";

            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
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
