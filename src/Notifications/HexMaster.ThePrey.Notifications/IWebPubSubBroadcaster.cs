namespace HexMaster.ThePrey.Notifications;

/// <summary>Pushes a real-time message to every client connected to a game's Web PubSub group.</summary>
public interface IWebPubSubBroadcaster
{
    /// <summary>
    /// Sends <paramref name="payload"/> to the group for <paramref name="gameId"/> wrapped in the
    /// canonical versioned envelope
    /// <c>{ v, type: <paramref name="eventType"/>, gameId, seq, data: <paramref name="payload"/> }</c>.
    /// The broadcaster allocates a monotonically increasing per-game <c>seq</c> so clients can detect
    /// dropped messages. Clients that joined the group on connect receive it.
    /// </summary>
    Task SendToGameAsync(Guid gameId, string eventType, object payload, CancellationToken ct);

    /// <summary>
    /// Broadcasts a <c>resync-requested</c> control message telling every client in the game's group
    /// to pull a fresh full snapshot, for use when a reliable incremental delta cannot be produced.
    /// </summary>
    Task RequestResyncAsync(Guid gameId, string reason, CancellationToken ct);
}
