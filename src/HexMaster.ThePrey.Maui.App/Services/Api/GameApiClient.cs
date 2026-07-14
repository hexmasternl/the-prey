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
}
