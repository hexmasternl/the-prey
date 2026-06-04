using ThePrey.Application.App.Models;

namespace ThePrey.Application.App.Services;

/// <summary>Typed HTTP client for the Games module endpoints used by the game engine.</summary>
public interface IGameService
{
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
