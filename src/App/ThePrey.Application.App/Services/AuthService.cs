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

    /// <summary>The user's display name from the OpenID profile, or null when not authenticated.</summary>
    string? DisplayName { get; }

    /// <summary>The user's profile picture URL from the OpenID profile, or null when unavailable.</summary>
    string? ProfilePictureUrl { get; }

    /// <summary>
    /// Returns a valid access token, transparently refreshing it when it is expired or within
    /// 30 seconds of expiry. Returns <c>null</c> when no token is available; the session is only
    /// cleared when Auth0 definitively rejects the refresh token (transient network failures
    /// keep the session for a later retry).
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

public sealed class AuthService(IHttpClientFactory httpClientFactory) : IAuthService
{
    private const string RefreshTokenKey = "auth0_refresh_token";
    private const int ExpiryBufferSeconds = 30;

    /// <summary>The outcome of a refresh-token exchange.</summary>
    private enum RefreshOutcome
    {
        /// <summary>A fresh access token was obtained and stored.</summary>
        Success,
        /// <summary>No refresh token is stored; an interactive login is required.</summary>
        NoSession,
        /// <summary>Auth0 rejected the refresh token (revoked/rotated away); the session is gone.</summary>
        Rejected,
        /// <summary>A network or server hiccup; the stored refresh token is kept for a retry.</summary>
        Transient
    }

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly HttpClient _http = httpClientFactory.CreateClient();

    public bool IsAuthenticated { get; private set; }
    public string? AccessToken { get; private set; }
    public string? DisplayName { get; private set; }
    public string? ProfilePictureUrl { get; private set; }

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

            return await RefreshSessionCoreAsync(ct) == RefreshOutcome.Success ? AccessToken : null;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task<bool> RestoreSessionAsync()
    {
        // Serialized with GetAccessTokenAsync: with refresh-token rotation enabled, two
        // concurrent exchanges of the same token trip Auth0's reuse detection, which revokes
        // the whole token family and permanently kills the remembered session.
        await _refreshLock.WaitAsync();
        try
        {
            if (IsAuthenticated && !IsTokenExpiredOrExpiringSoon(AccessToken))
                return true;

            return await RefreshSessionCoreAsync(CancellationToken.None) == RefreshOutcome.Success;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Exchanges the stored refresh token for a fresh access token via a direct
    /// <c>grant_type=refresh_token</c> call — the same raw OAuth style as the login code
    /// exchange (no OidcClient response validation involved). The refreshed access token keeps
    /// the API audience granted at login. Callers must hold <see cref="_refreshLock"/>.
    /// </summary>
    private async Task<RefreshOutcome> RefreshSessionCoreAsync(CancellationToken ct)
    {
        string? refreshToken;
        try
        {
            refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
        }
        catch
        {
            refreshToken = null;
        }

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            Clear();
            return RefreshOutcome.NoSession;
        }

        HttpResponseMessage response;
        string body;
        try
        {
            response = await _http.PostAsync(
                $"https://{MauiProgram.Auth0Domain}/oauth/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["client_id"] = MauiProgram.Auth0ClientId,
                    ["refresh_token"] = refreshToken,
                }), ct);
            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch
        {
            // Network hiccup: keep the stored refresh token (and any in-memory session) for a retry.
            return RefreshOutcome.Transient;
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                // 4xx (e.g. invalid_grant) means the token was revoked or rotated away — the
                // remembered session is definitively gone. 5xx is a server hiccup; retry later.
                if ((int)response.StatusCode is >= 400 and < 500)
                {
                    Clear();
                    SecureStorage.Default.Remove(RefreshTokenKey);
                    return RefreshOutcome.Rejected;
                }

                return RefreshOutcome.Transient;
            }
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var accessToken = doc.RootElement.GetProperty("access_token").GetString();
            var newRefreshToken = doc.RootElement.TryGetProperty("refresh_token", out var rt)
                ? rt.GetString() : null;
            var idToken = doc.RootElement.TryGetProperty("id_token", out var it)
                ? it.GetString() : null;

            if (string.IsNullOrWhiteSpace(accessToken))
                return RefreshOutcome.Transient;

            // With rotation enabled the response carries a new refresh token; store it so the
            // next refresh uses the live token instead of the consumed one.
            await StoreSessionAsync(accessToken, newRefreshToken, idToken);
            return RefreshOutcome.Success;
        }
        catch
        {
            return RefreshOutcome.Transient;
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
            var idToken = doc.RootElement.TryGetProperty("id_token", out var it)
                ? it.GetString() : null;

            if (string.IsNullOrWhiteSpace(accessToken))
                return false;

            await StoreSessionAsync(accessToken, refreshToken, idToken);
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

    private async Task StoreSessionAsync(string? accessToken, string? refreshToken, string? idToken = null)
    {
        AccessToken = accessToken;
        IsAuthenticated = true;
        CaptureProfile(idToken);

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

    /// <summary>Picks the display name and picture from the OpenID id_token claims, when present.</summary>
    private void CaptureProfile(string? idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            return;

        try
        {
            using var doc = DecodeJwtPayload(idToken);
            if (doc is null)
                return;

            var root = doc.RootElement;
            DisplayName = GetClaim(root, "name") ?? GetClaim(root, "nickname") ?? GetClaim(root, "email") ?? DisplayName;
            ProfilePictureUrl = GetClaim(root, "picture") ?? ProfilePictureUrl;
        }
        catch
        {
            // The profile is a nicety; an unparsable id_token must not break the session.
        }
    }

    private static string? GetClaim(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private void Clear()
    {
        IsAuthenticated = false;
        AccessToken = null;
        DisplayName = null;
        ProfilePictureUrl = null;
    }

    private static bool IsTokenExpiredOrExpiringSoon(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return true;

        try
        {
            using var doc = DecodeJwtPayload(token);
            if (doc is null || !doc.RootElement.TryGetProperty("exp", out var expElement))
                return true;

            var exp = DateTimeOffset.FromUnixTimeSeconds(expElement.GetInt64());
            return DateTimeOffset.UtcNow >= exp.AddSeconds(-ExpiryBufferSeconds);
        }
        catch
        {
            return true;
        }
    }

    /// <summary>Decodes a JWT's payload segment without validating the signature.</summary>
    private static JsonDocument? DecodeJwtPayload(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
            return null;

        var payload = parts[1].Replace("-", "+").Replace("_", "/");
        var remainder = payload.Length % 4;
        if (remainder == 2) payload += "==";
        else if (remainder == 3) payload += "=";

        return JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
    }
}
