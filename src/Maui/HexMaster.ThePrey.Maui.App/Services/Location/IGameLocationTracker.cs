namespace HexMaster.ThePrey.Maui.App.Services.Location;

/// <summary>
/// The cross-platform façade pages and view models use to start and stop background location reporting
/// for an in-progress game. Callers never touch platform types. Both operations are idempotent:
/// starting while already tracking the same game is a no-op, and stopping while not tracking is a no-op.
/// While tracking, the device position is reported to <c>POST /games/{id}/locations</c> on a server-driven
/// cadence for the full life of the game, continuing while the app is backgrounded or the screen is locked.
/// </summary>
public interface IGameLocationTracker
{
    /// <summary>
    /// Begins tracking for <paramref name="gameId"/>. No-op when already tracking that same game.
    /// Never throws — a failure to acquire background execution degrades to foreground-only reporting.
    /// </summary>
    Task StartAsync(Guid gameId, CancellationToken ct = default);

    /// <summary>Stops tracking and releases all background-execution resources. No-op when not tracking.</summary>
    Task StopAsync();
}
