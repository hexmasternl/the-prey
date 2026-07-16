using HexMaster.ThePrey.Maui.App.Services.Api;

namespace HexMaster.ThePrey.Maui.App.Services.Realtime;

/// <summary>
/// The app's single source of truth for the active game. Owns one <see cref="IGameRealtimeConnection"/>,
/// keeps the authoritative <see cref="CurrentState"/> snapshot up to date by applying real-time events,
/// reconciles a full snapshot on every (re)connect, and notifies subscribers on every change. Consumers
/// read <see cref="CurrentState"/> and/or subscribe rather than opening their own connections.
/// </summary>
public interface IGameStateService
{
    /// <summary>The latest known game state, or <c>null</c> before the first snapshot has been fetched.</summary>
    GameDetails? CurrentState { get; }

    /// <summary>Starts synchronizing the given game. Idempotent via the underlying connection.</summary>
    void Start(Guid gameId);

    /// <summary>Stops synchronizing and tears down the connection.</summary>
    Task StopAsync();

    /// <summary>Registers a handler invoked with the current state whenever it changes.</summary>
    void Subscribe(Action<GameStateChanged> handler);

    /// <summary>Removes a previously registered handler.</summary>
    void Unsubscribe(Action<GameStateChanged> handler);
}
