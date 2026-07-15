using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using HexMaster.ThePrey.Maui.App.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HexMaster.ThePrey.Maui.App.Services.Authentication;

/// <summary>
/// <see cref="IAuth0TokenClient"/> over a typed <see cref="HttpClient"/>. Uses the OAuth
/// <c>refresh_token</c> and <c>authorization_code</c> (PKCE) grants against Auth0's
/// <c>/oauth/token</c> endpoint. The client is public (no secret) per native-app best practice.
/// </summary>
public sealed class Auth0TokenClient : IAuth0TokenClient
{
    private readonly HttpClient _http;
    private readonly ThePreyClientOptions _options;
    private readonly ILogger<Auth0TokenClient> _logger;

    public Auth0TokenClient(HttpClient http, IOptions<ThePreyClientOptions> options, ILogger<Auth0TokenClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public Task<Auth0TokenResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        // Auth0's refresh_token grant does not take an `audience` parameter — the refresh token is
        // already bound to the audience requested at authorize time (see ExchangeCodeAsync).
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _options.Auth0ClientId,
            ["refresh_token"] = refreshToken
        };
        return PostAsync(form, ct);
    }

    public Task<Auth0TokenResult> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = _options.Auth0ClientId,
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["redirect_uri"] = _options.RedirectUri
        };
        return PostAsync(form, ct);
    }

    private async Task<Auth0TokenResult> PostAsync(IDictionary<string, string> form, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            using var content = new FormUrlEncodedContent(form);
            response = await _http.PostAsync(_options.TokenEndpoint, content, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Auth0 token request failed to complete (transient).");
            return Auth0TokenResult.Transient;
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                var payload = TryParse(body);
                if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
                {
                    _logger.LogWarning("Auth0 token response was successful but contained no access token. Body: {Body}", body);
                    return Auth0TokenResult.Transient;
                }

                if (string.IsNullOrWhiteSpace(payload.RefreshToken))
                {
                    // Access token was issued but NO refresh token. Almost always means the Auth0 API
                    // (audience) does not have "Allow Offline Access" enabled, or the app is not allowed
                    // the offline_access scope. Surface this explicitly — the caller needs a refresh token.
                    _logger.LogWarning(
                        "Auth0 returned an access token but no refresh token. Enable 'Allow Offline Access' " +
                        "on the API '{Audience}' in the Auth0 dashboard and re-login.", _options.Audience);
                }

                return Auth0TokenResult.FromSuccess(payload.AccessToken, payload.RefreshToken);
            }

            // 4xx (invalid_grant, invalid_request, unauthorized_client, ...) means the grant is dead —
            // clearing the stored refresh token is the correct response. 5xx is transient.
            if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
            {
                _logger.LogWarning("Auth0 rejected the token exchange with status {Status}. Body: {Body}", response.StatusCode, body);
                return Auth0TokenResult.Rejected;
            }

            _logger.LogWarning("Auth0 token endpoint returned server error {Status}. Body: {Body}", response.StatusCode, body);
            return Auth0TokenResult.Transient;
        }
    }

    private TokenResponse? TryParse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Auth0 token response.");
            return null;
        }
    }

    private sealed record TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; init; }
    }
}
