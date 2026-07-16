using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Maui.App.Services.Api;

/// <summary>
/// <see cref="IGameApiClient"/> over a typed <see cref="HttpClient"/> whose <c>BaseAddress</c> and
/// timeout are configured in DI. Maps the backend status codes for <c>GET /games/active</c>:
/// <c>200</c> → active game, <c>404</c> → no active game, <c>401</c> → unauthenticated.
/// </summary>
public sealed class GameApiClient : IGameApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<GameApiClient> _logger;

    public GameApiClient(HttpClient http, ILogger<GameApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<ActiveGameResult> GetActiveGameAsync(string accessToken, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "games/active");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Active-game request failed to complete.");
            return ActiveGameResult.Error;
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    try
                    {
                        var game = await response.Content.ReadFromJsonAsync<GameStatus>(cancellationToken: ct);
                        return game is null ? ActiveGameResult.Error : ActiveGameResult.Active(game);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize active-game payload.");
                        return ActiveGameResult.Error;
                    }

                case HttpStatusCode.NotFound:
                    return ActiveGameResult.None;

                case HttpStatusCode.Unauthorized:
                    return ActiveGameResult.Unauthorized;

                default:
                    _logger.LogWarning("Active-game endpoint returned unexpected status {Status}.", response.StatusCode);
                    return ActiveGameResult.Error;
            }
        }
    }

    public async Task<CreateGameResult> CreateGameAsync(CreateGameParameters request, string accessToken, CancellationToken ct = default)
    {
        // Durations are minutes; the two location intervals are already in seconds (the VM converted them).
        // The boundary-penalty toggles and profile-picture url are sent as their contract defaults.
        var body = new CreateGameBody(
            request.PlayfieldId,
            request.DisplayName,
            request.GameDurationMinutes,
            request.HeadstartMinutes,
            request.EndgameMinutes,
            request.DefaultLocationIntervalSeconds,
            request.FinalLocationIntervalSeconds,
            EnablePreyBoundaryPenalties: false,
            EnableHunterBoundaryPenalty: false,
            ProfilePictureUrl: null);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "games")
        {
            Content = JsonContent.Create(body)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(httpRequest, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Create-game request failed to complete.");
            return CreateGameResult.Error;
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.Created:
                case HttpStatusCode.OK:
                    try
                    {
                        var game = await response.Content.ReadFromJsonAsync<GameSummary>(cancellationToken: ct);
                        return game is null || game.Id == Guid.Empty
                            ? CreateGameResult.Error
                            : CreateGameResult.Success(game);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize create-game payload.");
                        return CreateGameResult.Error;
                    }

                case HttpStatusCode.BadRequest:
                    return CreateGameResult.Validation;

                case HttpStatusCode.Unauthorized:
                    return CreateGameResult.Unauthorized;

                default:
                    _logger.LogWarning("Create-game endpoint returned unexpected status {Status}.", response.StatusCode);
                    return CreateGameResult.Error;
            }
        }
    }

    public async Task<JoinGameResult> JoinGameAsync(
        Guid gameId, string joinCode, string displayName, string accessToken, CancellationToken ct = default)
    {
        var body = new JoinGameBody(joinCode, displayName, ProfilePictureUrl: null);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"games/{gameId}/join")
        {
            Content = JsonContent.Create(body)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(httpRequest, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Join-game request failed to complete.");
            return JoinGameResult.Error;
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    try
                    {
                        var game = await response.Content.ReadFromJsonAsync<GameSummary>(cancellationToken: ct);
                        return game is null || game.Id == Guid.Empty
                            ? JoinGameResult.Error
                            : JoinGameResult.Success(game);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize join-game payload.");
                        return JoinGameResult.Error;
                    }

                case HttpStatusCode.BadRequest:
                    return JoinGameResult.InvalidCode(await ReadProblemCodeAsync(response, ct));

                case HttpStatusCode.NotFound:
                    return JoinGameResult.NotFound;

                case HttpStatusCode.Conflict:
                    return JoinGameResult.Conflict(await ReadProblemCodeAsync(response, ct));

                case HttpStatusCode.Unauthorized:
                    return JoinGameResult.Unauthorized;

                default:
                    _logger.LogWarning("Join-game endpoint returned unexpected status {Status}.", response.StatusCode);
                    return JoinGameResult.Error;
            }
        }
    }

    // Reads the stable machine-readable rule code the backend attaches to its ProblemDetails body (an
    // extension member serialized at the document root) for a 400/409. Returns null when absent/unreadable.
    private async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemCodeBody>(cancellationToken: ct);
            return problem?.Code;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read the ProblemDetails code from the join response.");
            return null;
        }
    }

    public async Task<GetGameResult> GetGameAsync(Guid gameId, string accessToken, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"games/{gameId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Get-game request failed to complete.");
            return GetGameResult.Error;
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    try
                    {
                        var game = await response.Content.ReadFromJsonAsync<GameDetails>(cancellationToken: ct);
                        return game is null ? GetGameResult.Error : GetGameResult.Success(game);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize get-game payload.");
                        return GetGameResult.Error;
                    }

                case HttpStatusCode.NotFound:
                    return GetGameResult.NotFound;

                case HttpStatusCode.Unauthorized:
                    return GetGameResult.Unauthorized;

                default:
                    _logger.LogWarning("Get-game endpoint returned unexpected status {Status}.", response.StatusCode);
                    return GetGameResult.Error;
            }
        }
    }

    public async Task<UpdateGameSettingsResult> UpdateGameSettingsAsync(
        Guid gameId, GameSettingsParameters settings, string accessToken, CancellationToken ct = default)
    {
        // The two ping intervals are entered in minutes but the backend stores them in seconds.
        var body = new UpdateGameSettingsBody(
            settings.GameDurationMinutes,
            settings.HeadstartMinutes,
            settings.EndgameMinutes,
            settings.PingMinutes * 60,
            settings.EndgamePingMinutes * 60);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"games/{gameId}/config")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Update-game-settings request failed to complete.");
            return UpdateGameSettingsResult.Error;
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    try
                    {
                        var game = await response.Content.ReadFromJsonAsync<GameDetails>(cancellationToken: ct);
                        return game is null ? UpdateGameSettingsResult.Error : UpdateGameSettingsResult.Success(game);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize update-game-settings payload.");
                        return UpdateGameSettingsResult.Error;
                    }

                case HttpStatusCode.BadRequest:
                    return UpdateGameSettingsResult.Validation;

                case HttpStatusCode.Forbidden:
                    return UpdateGameSettingsResult.Forbidden;

                case HttpStatusCode.Unauthorized:
                    return UpdateGameSettingsResult.Unauthorized;

                default:
                    _logger.LogWarning("Update-game-settings endpoint returned unexpected status {Status}.", response.StatusCode);
                    return UpdateGameSettingsResult.Error;
            }
        }
    }

    public async Task<DesignateHunterResult> DesignateHunterAsync(
        Guid gameId, Guid newHunterUserId, string accessToken, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"games/{gameId}/hunter")
        {
            Content = JsonContent.Create(new SetHunterBody(newHunterUserId))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Designate-hunter request failed to complete.");
            return DesignateHunterResult.Error;
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    try
                    {
                        var game = await response.Content.ReadFromJsonAsync<GameDetails>(cancellationToken: ct);
                        return game is null ? DesignateHunterResult.Error : DesignateHunterResult.Success(game);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize designate-hunter payload.");
                        return DesignateHunterResult.Error;
                    }

                case HttpStatusCode.Forbidden:
                    return DesignateHunterResult.Forbidden;

                case HttpStatusCode.NotFound:
                    return DesignateHunterResult.NotFound;

                case HttpStatusCode.Unauthorized:
                    return DesignateHunterResult.Unauthorized;

                default:
                    _logger.LogWarning("Designate-hunter endpoint returned unexpected status {Status}.", response.StatusCode);
                    return DesignateHunterResult.Error;
            }
        }
    }

    public async Task<SetReadyResult> SetReadyAsync(Guid gameId, string accessToken, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"games/{gameId}/lobby/ready");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Set-ready request failed to complete.");
            return SetReadyResult.Error;
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    try
                    {
                        var game = await response.Content.ReadFromJsonAsync<GameDetails>(cancellationToken: ct);
                        return game is null ? SetReadyResult.Error : SetReadyResult.Success(game);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize set-ready payload.");
                        return SetReadyResult.Error;
                    }

                case HttpStatusCode.Forbidden:
                    return SetReadyResult.Forbidden;

                case HttpStatusCode.NotFound:
                    return SetReadyResult.NotFound;

                case HttpStatusCode.Unauthorized:
                    return SetReadyResult.Unauthorized;

                default:
                    _logger.LogWarning("Set-ready endpoint returned unexpected status {Status}.", response.StatusCode);
                    return SetReadyResult.Error;
            }
        }
    }

    public async Task<StartGameResult> StartGameAsync(
        Guid gameId, Guid hunterUserId, string accessToken, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"games/{gameId}/start")
        {
            Content = JsonContent.Create(new StartGameBody(hunterUserId))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Start-game request failed to complete.");
            return StartGameResult.Error;
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    try
                    {
                        var game = await response.Content.ReadFromJsonAsync<GameDetails>(cancellationToken: ct);
                        return game is null ? StartGameResult.Error : StartGameResult.Success(game);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize start-game payload.");
                        return StartGameResult.Error;
                    }

                case HttpStatusCode.BadRequest:
                    return StartGameResult.Validation;

                case HttpStatusCode.Forbidden:
                    return StartGameResult.Forbidden;

                case HttpStatusCode.NotFound:
                    return StartGameResult.NotFound;

                case HttpStatusCode.Unauthorized:
                    return StartGameResult.Unauthorized;

                default:
                    _logger.LogWarning("Start-game endpoint returned unexpected status {Status}.", response.StatusCode);
                    return StartGameResult.Error;
            }
        }
    }

    public async Task<GameStatusResult> GetGameStatusAsync(Guid gameId, string accessToken, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"games/{gameId}/status");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Game-status request failed to complete.");
            return GameStatusResult.Error;
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    try
                    {
                        var status = await response.Content.ReadFromJsonAsync<GameStatusSnapshot>(cancellationToken: ct);
                        return status is null ? GameStatusResult.Error : GameStatusResult.Success(status);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize game-status payload.");
                        return GameStatusResult.Error;
                    }

                case HttpStatusCode.NotFound:
                    return GameStatusResult.NotFound;

                case HttpStatusCode.Forbidden:
                    return GameStatusResult.Forbidden;

                case HttpStatusCode.Conflict:
                    return GameStatusResult.Completed;

                case HttpStatusCode.Unauthorized:
                    return GameStatusResult.Unauthorized;

                default:
                    _logger.LogWarning("Game-status endpoint returned unexpected status {Status}.", response.StatusCode);
                    return GameStatusResult.Error;
            }
        }
    }

    public async Task<GetGameStatusResult> GetGameStatusDetailsAsync(Guid gameId, string accessToken, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"games/{gameId}/status");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Game-status (details) request failed to complete.");
            return GetGameStatusResult.Error;
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    try
                    {
                        var details = await response.Content.ReadFromJsonAsync<GameStatusDetails>(cancellationToken: ct);
                        return details is null ? GetGameStatusResult.Error : GetGameStatusResult.Success(details);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize game-status (details) payload.");
                        return GetGameStatusResult.Error;
                    }

                case HttpStatusCode.Forbidden:
                    return GetGameStatusResult.Forbidden;

                case HttpStatusCode.Conflict:
                    return GetGameStatusResult.Conflict;

                case HttpStatusCode.NotFound:
                    return GetGameStatusResult.NotFound;

                case HttpStatusCode.Unauthorized:
                    return GetGameStatusResult.Unauthorized;

                default:
                    _logger.LogWarning("Game-status (details) endpoint returned unexpected status {Status}.", response.StatusCode);
                    return GetGameStatusResult.Error;
            }
        }
    }

    public async Task<GameStateResult> GetGameStateAsync(Guid gameId, string accessToken, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"games/{gameId}/state");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Game-state request failed to complete.");
            return GameStateResult.Error;
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    try
                    {
                        var state = await response.Content.ReadFromJsonAsync<GameStateSnapshot>(cancellationToken: ct);
                        return state is null ? GameStateResult.Error : GameStateResult.Success(state);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize game-state payload.");
                        return GameStateResult.Error;
                    }

                case HttpStatusCode.NotFound:
                    return GameStateResult.NotFound;

                case HttpStatusCode.Unauthorized:
                    return GameStateResult.Unauthorized;

                default:
                    _logger.LogWarning("Game-state endpoint returned unexpected status {Status}.", response.StatusCode);
                    return GameStateResult.Error;
            }
        }
    }

    public async Task<TagCandidatesResult> GetTagCandidatesAsync(Guid gameId, string accessToken, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"games/{gameId}/tag-candidates");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Tag-candidates request failed to complete.");
            return TagCandidatesResult.Error;
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    try
                    {
                        var body = await response.Content.ReadFromJsonAsync<TagCandidatesBody>(cancellationToken: ct);
                        return body is null
                            ? TagCandidatesResult.Error
                            : TagCandidatesResult.Success(body.Candidates ?? Array.Empty<TagCandidate>(), body.RangeMeters);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize tag-candidates payload.");
                        return TagCandidatesResult.Error;
                    }

                case HttpStatusCode.Forbidden:
                    return TagCandidatesResult.Forbidden;

                case HttpStatusCode.NotFound:
                    return TagCandidatesResult.NotFound;

                case HttpStatusCode.Unauthorized:
                    return TagCandidatesResult.Unauthorized;

                default:
                    _logger.LogWarning("Tag-candidates endpoint returned unexpected status {Status}.", response.StatusCode);
                    return TagCandidatesResult.Error;
            }
        }
    }

    public async Task<TagPlayerResult> TagPlayerAsync(
        Guid gameId, Guid participantId, string accessToken, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"games/{gameId}/participants/{participantId}/tag");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Tag-player request failed to complete.");
            return TagPlayerResult.Error;
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.NoContent:
                case HttpStatusCode.OK:
                    return TagPlayerResult.Success;

                case HttpStatusCode.Forbidden:
                    return TagPlayerResult.Forbidden;

                case HttpStatusCode.NotFound:
                    return TagPlayerResult.NotFound;

                case HttpStatusCode.Conflict:
                    return TagPlayerResult.Conflict;

                case HttpStatusCode.Unauthorized:
                    return TagPlayerResult.Unauthorized;

                default:
                    _logger.LogWarning("Tag-player endpoint returned unexpected status {Status}.", response.StatusCode);
                    return TagPlayerResult.Error;
            }
        }
    }

    public async Task<NotificationsTokenResult> GetNotificationsTokenAsync(
        Guid gameId, string accessToken, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"games/{gameId}/notifications/token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Notifications-token request failed to complete.");
            return NotificationsTokenResult.Error;
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    try
                    {
                        var body = await response.Content.ReadFromJsonAsync<NotificationsTokenBody>(cancellationToken: ct);
                        return string.IsNullOrWhiteSpace(body?.Url)
                            ? NotificationsTokenResult.Error
                            : NotificationsTokenResult.Success(body!.Url!);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize notifications-token payload.");
                        return NotificationsTokenResult.Error;
                    }

                case HttpStatusCode.Forbidden:
                    return NotificationsTokenResult.Forbidden;

                case HttpStatusCode.Unauthorized:
                    return NotificationsTokenResult.Unauthorized;

                default:
                    _logger.LogWarning("Notifications-token endpoint returned unexpected status {Status}.", response.StatusCode);
                    return NotificationsTokenResult.Error;
            }
        }
    }

    // Request bodies — serialized with the default web (camelCase) options to match the backend records.
    // Mirrors the backend CreateGameRequest; the trailing three fields carry the contract defaults.
    private sealed record CreateGameBody(
        Guid PlayfieldId, string DisplayName, int GameDuration, int HunterDelayTime, int FinalStageDuration,
        int DefaultLocationInterval, int FinalLocationInterval,
        bool EnablePreyBoundaryPenalties, bool EnableHunterBoundaryPenalty, string? ProfilePictureUrl);

    private sealed record UpdateGameSettingsBody(
        int GameDuration, int HunterDelayTime, int FinalStageDuration,
        int DefaultLocationInterval, int FinalLocationInterval);

    // Mirrors the backend JoinGameRequest; the profile-picture url is sent as its contract default.
    private sealed record JoinGameBody(string JoinCode, string DisplayName, string? ProfilePictureUrl);

    // Minimal ProblemDetails shape — only the stable rule `code` extension member the page maps to a message.
    private sealed record ProblemCodeBody(string? Code);

    private sealed record SetHunterBody(Guid NewHunterUserId);

    private sealed record StartGameBody(Guid HunterUserId);

    // Response shape for GET /games/{id}/tag-candidates (TagCandidatesDto), bound case-insensitively.
    private sealed record TagCandidatesBody(double RangeMeters, IReadOnlyList<TagCandidate>? Candidates);

    // Response shape for GET /games/{id}/notifications/token (GameNotificationConnectionDto → { url }).
    private sealed record NotificationsTokenBody(string? Url);
}
