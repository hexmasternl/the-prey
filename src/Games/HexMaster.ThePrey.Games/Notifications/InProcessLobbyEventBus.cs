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
    private readonly ConcurrentDictionary<Guid, Channel<LobbyEvent>> _channels = new();

    public IAsyncEnumerable<LobbyEvent> Subscribe(Guid gameId)
    {
        var channel = _channels.GetOrAdd(gameId, _ => Channel.CreateUnbounded<LobbyEvent>());
        return channel.Reader.ReadAllAsync();
    }

    public ValueTask PublishAsync(Guid gameId, string eventType, GameDto payload, CancellationToken ct = default)
    {
        if (_channels.TryGetValue(gameId, out var channel))
            return channel.Writer.WriteAsync(new LobbyEvent(gameId, eventType, payload), ct);

        return ValueTask.CompletedTask;
    }

    public void Complete(Guid gameId)
    {
        if (_channels.TryRemove(gameId, out var channel))
            channel.Writer.TryComplete();
    }
}
