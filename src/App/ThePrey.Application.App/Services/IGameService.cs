using ThePrey.Application.App.Models;

namespace ThePrey.Application.App.Services;

/// <summary>Typed HTTP client for the Games module endpoints.</summary>
public interface IGameService
{
    /// <summary>
    /// Creates a game on the server with the given options; the caller joins the lobby as its
    /// first player. Returns the created game, including its 8-digit game code.
    /// </summary>
    /// <exception cref="UnauthorizedException">The session could not be recovered.</exception>
    Task<Game> CreateGameAsync(CreateGameOptions options, CancellationToken ct = default);

    /// <summary>Fetches a game by id. Returns null when the game no longer exists (HTTP 404).</summary>
    /// <exception cref="UnauthorizedException">The session could not be recovered.</exception>
    Task<Game?> GetGameAsync(Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Starts a game, designating the given lobby member as the hunter. Returns the started game,
    /// or null when the game no longer exists (HTTP 404).
    /// </summary>
    /// <exception cref="UnauthorizedException">The session could not be recovered.</exception>
    Task<Game?> StartGameAsync(Guid gameId, Guid hunterUserId, CancellationToken ct = default);

    /// <summary>
    /// Pushes the device's current GPS coordinates to the server.
    /// Returns null when the game no longer exists (HTTP 404).
    /// </summary>
    /// <exception cref="UnauthorizedException">The session could not be recovered.</exception>
    Task<LocationPushResponse?> PushLocationAsync(
        string gameId, double latitude, double longitude, double? accuracy, CancellationToken ct = default);

    /// <summary>
    /// Pulls the role-specific game state. Returns null when the game no longer exists or has
    /// ended (HTTP 404).
    /// </summary>
    /// <exception cref="UnauthorizedException">The session could not be recovered.</exception>
    Task<GameStateSnapshot?> GetGameStateAsync(string gameId, CancellationToken ct = default);
}
