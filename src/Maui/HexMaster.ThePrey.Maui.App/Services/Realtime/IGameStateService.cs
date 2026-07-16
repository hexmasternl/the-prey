namespace HexMaster.ThePrey.Maui.App.Services.Realtime;

/// <summary>
/// The app's single source of truth for the active game while it is being played. Owns one
/// <see cref="IGameRealtimeConnection"/>, keeps the authoritative <see cref="CurrentState"/> composite up
/// to date by applying real-time events, reconciles a full snapshot on every (re)connect and on a periodic
/// heartbeat, and notifies subscribers on every change. The map and HUD read <see cref="CurrentState"/> and
/// subscribe rather than opening their own connections or polling.
/// </summary>
public interface IGameStateService
{
    /// <summary>The latest composite state, or <c>null</c> before the first snapshot has been fetched.</summary>
    GameLiveState? CurrentState { get; }

    /// <summary>
    /// Resolves the caller's active game, seeds the first snapshot, starts the periodic reconcile, and opens
    /// the live connection. Returns the seeded state, or <c>null</c> when there is no active game (or it
    /// could not be loaded / the caller is unauthorized).
    /// </summary>
    Task<GameLiveState?> StartAsync(CancellationToken ct = default);

    /// <summary>
    /// Starts synchronizing a known game: opens the live connection and arms the periodic reconcile. The
    /// first authoritative snapshot arrives via the connection's <c>Connected</c> reconcile. Idempotent via
    /// the underlying connection.
    /// </summary>
    void Start(Guid gameId);

    /// <summary>Stops the periodic reconcile and tears down the connection.</summary>
    Task StopAsync();

    /// <summary>Registers a handler invoked with the current state whenever it changes.</summary>
    void Subscribe(Action<GameStateChanged> handler);

    /// <summary>Removes a previously registered handler.</summary>
    void Unsubscribe(Action<GameStateChanged> handler);
}
