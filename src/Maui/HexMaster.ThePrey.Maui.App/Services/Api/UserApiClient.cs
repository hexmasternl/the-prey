using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>
/// <see cref="IUserApiClient"/> over a typed <see cref="HttpClient"/> whose <c>BaseAddress</c> and
/// timeout are configured in DI. Maps the backend status codes for the authorized users endpoints:
/// <c>200</c> → success, <c>400</c> → validation failed (save), <c>401</c> → unauthenticated,
/// <c>404</c> → user not found, network/timeout/other → error. Mirrors <see cref="GameApiClient"/>.
/// </summary>
public sealed class UserApiClient : IUserApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<UserApiClient> _logger;

    public UserApiClient(HttpClient http, ILogger<UserApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<UserSettingsResult> GetCurrentUserAsync(string accessToken, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Get-current-user request failed to complete.");
            return UserSettingsResult.Error;
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    try
                    {
                        var user = await response.Content.ReadFromJsonAsync<UserPayload>(cancellationToken: ct);
                        return user is null
                            ? UserSettingsResult.Error
                            : UserSettingsResult.Success(user.ToSettings());
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize current-user payload.");
                        return UserSettingsResult.Error;
                    }

                case HttpStatusCode.Unauthorized:
                    return UserSettingsResult.Unauthorized;

                case HttpStatusCode.NotFound:
                    return UserSettingsResult.NotFound;

                default:
                    _logger.LogWarning("Get-current-user endpoint returned unexpected status {Status}.", response.StatusCode);
                    return UserSettingsResult.Error;
            }
        }
    }

    public async Task<SaveSettingsResult> UpdateUserAsync(UserSettings settings, string accessToken, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "users/me")
        {
            Content = JsonContent.Create(new UpdateUserPayload(
                FirstName: null,
                LastName: null,
                DisplayName: settings.DisplayName,
                PreferredLanguage: settings.PreferredLanguage))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Update-user request failed to complete.");
            return SaveSettingsResult.Error;
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    try
                    {
                        var user = await response.Content.ReadFromJsonAsync<UserPayload>(cancellationToken: ct);
                        // The backend echoes the persisted user; if the body is missing, treat the sent
                        // values as authoritative rather than failing an otherwise-successful save.
                        return SaveSettingsResult.Success(user?.ToSettings() ?? settings);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize updated-user payload.");
                        return SaveSettingsResult.Success(settings);
                    }

                case HttpStatusCode.BadRequest:
                    return SaveSettingsResult.ValidationFailed;

                case HttpStatusCode.Unauthorized:
                    return SaveSettingsResult.Unauthorized;

                case HttpStatusCode.NotFound:
                    return SaveSettingsResult.NotFound;

                default:
                    _logger.LogWarning("Update-user endpoint returned unexpected status {Status}.", response.StatusCode);
                    return SaveSettingsResult.Error;
            }
        }
    }

    /// <summary>Wire model for the backend <c>UserDto</c> (camelCase JSON).</summary>
    private sealed record UserPayload
    {
        // The caller's internal user id. Must be carried through to UserSettings: it is the app's only
        // source for "who am I", and every id comparison depends on it — the gameplay hand-off's
        // hunter-vs-prey branch (GameplayRouter), the lobby's ownership derivation off a shared
        // broadcast, and the outcome page's role. Dropping it silently routes every hunter to the
        // prey page, because ICurrentUserProvider then resolves no id at all.
        [JsonPropertyName("userId")]
        public Guid UserId { get; init; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; init; }

        [JsonPropertyName("preferredLanguage")]
        public string? PreferredLanguage { get; init; }

        public UserSettings ToSettings() =>
            new(DisplayName ?? string.Empty, PreferredLanguage ?? string.Empty, UserId);
    }

    /// <summary>Wire model for the backend <c>UpdateUserRequest</c> (camelCase JSON).</summary>
    private sealed record UpdateUserPayload(
        [property: JsonPropertyName("firstName")] string? FirstName,
        [property: JsonPropertyName("lastName")] string? LastName,
        [property: JsonPropertyName("displayName")] string DisplayName,
        [property: JsonPropertyName("preferredLanguage")] string PreferredLanguage);
}
