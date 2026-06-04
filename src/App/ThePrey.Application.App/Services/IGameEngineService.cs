using ThePrey.Application.App.Models;

namespace ThePrey.Application.App.Services;

/// <summary>
/// The background game engine: pushes the device's GPS location to the server at the
/// server-controlled interval and pulls role-specific game state, exposing it via
/// <see cref="GameStateContext"/>. Started when a game session begins, stopped when it ends.
/// </summary>
public interface IGameEngineService
{
    /// <summary>Starts the game loops for the given game. Idempotent while already running.</summary>
    Task StartAsync(string gameId, PlayerRole role);

    /// <summary>Stops the game loops and waits until they have drained.</summary>
    Task StopAsync();

    /// <summary>
    /// Suspends the loops while the app is backgrounded. The game session itself stays active so
    /// <see cref="ResumeAsync"/> can restart the loops. Wired to the window lifecycle in <c>App</c>.
    /// </summary>
    Task SuspendAsync();

    /// <summary>Restarts the loops on app foreground when a game session is still active.</summary>
    Task ResumeAsync();
}
