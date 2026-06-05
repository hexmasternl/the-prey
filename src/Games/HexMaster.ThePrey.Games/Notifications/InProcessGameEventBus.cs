using System.Collections.Concurrent;
using System.Threading.Channels;

namespace HexMaster.ThePrey.Games.Notifications;

/// <summary>
/// In-process event bus for gameplay events. Each game gets an unbounded channel;
/// subscribers read all events via <see cref="Subscribe"/>. Channels are created lazily on first
/// subscribe and torn down when the game ends or the SSE connection closes.
/// </summary>
public sealed class InProcessGameEventBus : IGameEventBus
{
    private readonly ConcurrentDictionary<Guid, Channel<GameEvent>> _channels = new();

    public IAsyncEnumerable<GameEvent> Subscribe(Guid gameId)
    {
        var channel = _channels.GetOrAdd(gameId, _ => Channel.CreateUnbounded<GameEvent>());
        return channel.Reader.ReadAllAsync();
    }

    public ValueTask PublishAsync(Guid gameId, GameEvent evt, CancellationToken ct = default)
    {
        if (_channels.TryGetValue(gameId, out var channel))
            return channel.Writer.WriteAsync(evt, ct);

        return ValueTask.CompletedTask;
    }

    public void Complete(Guid gameId)
    {
        if (_channels.TryRemove(gameId, out var channel))
            channel.Writer.TryComplete();
    }
}
