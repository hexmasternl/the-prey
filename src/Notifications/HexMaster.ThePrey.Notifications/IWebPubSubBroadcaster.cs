namespace HexMaster.ThePrey.Notifications;

/// <summary>Pushes a real-time message to every client connected to a game's Web PubSub group.</summary>
public interface IWebPubSubBroadcaster
{
    /// <summary>
    /// Sends <paramref name="payload"/> (serialized as a typed envelope) to the group for
    /// <paramref name="gameId"/>. Clients that joined the group on connect receive it.
    /// </summary>
    Task SendToGameAsync(Guid gameId, string eventType, object payload, CancellationToken ct);
}
