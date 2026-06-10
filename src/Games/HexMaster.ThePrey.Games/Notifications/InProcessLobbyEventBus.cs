using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

namespace HexMaster.ThePrey.Games.Notifications;

/// <summary>
/// In-process event bus for lobby state changes. Each game gets an unbounded channel;
/// subscribers read all events via <see cref="Subscribe"/>. Channels are created lazily on first
/// subscribe and torn down by the SSE endpoint when the client disconnects.
/// </summary>
public sealed class InProcessLobbyEventBus : ILobbyEventBus
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Channel<LobbyEvent>>> _subscribers = new();

    public IAsyncEnumerable<LobbyEvent> Subscribe(Guid gameId)
    {
        var gameSubscribers = _subscribers.GetOrAdd(gameId, _ => new ConcurrentDictionary<Guid, Channel<LobbyEvent>>());
        var subscriberId = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<LobbyEvent>();
        gameSubscribers[subscriberId] = channel;
        return ReadSubscription(gameId, subscriberId, channel.Reader);
    }

    public ValueTask PublishAsync(Guid gameId, string eventType, GameDto payload, CancellationToken ct = default)
    {
        if (_subscribers.TryGetValue(gameId, out var gameSubscribers))
        {
            var evt = new LobbyEvent(gameId, eventType, payload);
            foreach (var kvp in gameSubscribers)
            {
                if (!kvp.Value.Writer.TryWrite(evt))
                    RemoveSubscriber(gameId, kvp.Key);
            }
        }

        return ValueTask.CompletedTask;
    }

    public void Complete(Guid gameId)
    {
        if (_subscribers.TryRemove(gameId, out var gameSubscribers))
        {
            foreach (var subscriber in gameSubscribers.Values)
                subscriber.Writer.TryComplete();
        }
    }

    private async IAsyncEnumerable<LobbyEvent> ReadSubscription(
        Guid gameId,
        Guid subscriberId,
        ChannelReader<LobbyEvent> reader,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        try
        {
            await foreach (var evt in reader.ReadAllAsync(ct))
                yield return evt;
        }
        finally
        {
            RemoveSubscriber(gameId, subscriberId);
        }
    }

    private void RemoveSubscriber(Guid gameId, Guid subscriberId)
    {
        if (_subscribers.TryGetValue(gameId, out var gameSubscribers))
        {
            if (gameSubscribers.TryRemove(subscriberId, out var channel))
                channel.Writer.TryComplete();

            if (gameSubscribers.IsEmpty)
                _subscribers.TryRemove(gameId, out _);
        }
    }
}
