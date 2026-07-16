namespace HexMaster.ThePrey.Maui.App.Services.Realtime;

/// <summary>
/// Owns a single Web PubSub WebSocket for one game: it fetches a group-scoped access URL, connects,
/// joins the game's group, surfaces each group message as a <see cref="GameRealtimeEnvelope"/>, and
/// reconnects with backoff on drop. It holds no game state — <see cref="IGameStateService"/> layers that
/// on top by subscribing to these events.
/// </summary>
public interface IGameRealtimeConnection
{
    /// <summary>Raised for every real-time event received from the game's group.</summary>
    event Action<GameRealtimeEnvelope>? EnvelopeReceived;

    /// <summary>Raised once after the first successful connect + group join.</summary>
    event Action? Connected;

    /// <summary>Raised after each successful re-join following an unexpected drop.</summary>
    event Action? Reconnected;

    /// <summary>Raised when access is permanently denied (the caller is not a member of the game).</summary>
    event Action? Unavailable;

    /// <summary>Starts the connection for <paramref name="gameId"/>. Idempotent: a second call is a no-op.</summary>
    void Start(Guid gameId);

    /// <summary>Closes the socket, cancels any pending reconnect, and stops raising events.</summary>
    Task StopAsync();
}
