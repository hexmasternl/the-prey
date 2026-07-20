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
    /// <param name="gameId">The in-progress game to report positions for.</param>
    /// <param name="remaining">
    /// How long the game still has to run when tracking starts. The tracker stops itself once this elapses,
    /// so background reporting always terminates at game end even if the server's game-over signal never
    /// arrives (e.g. the app is backgrounded and the live channel is disconnected). <c>null</c> (or a
    /// non-positive value) means no deadline — tracking then ends only on an explicit stop or a server
    /// game-over signal.
    /// </param>
    /// <param name="ct">Cancels the start operation.</param>
    Task StartAsync(Guid gameId, TimeSpan? remaining = null, CancellationToken ct = default);

    /// <summary>Stops tracking and releases all background-execution resources. No-op when not tracking.</summary>
    Task StopAsync();
}
