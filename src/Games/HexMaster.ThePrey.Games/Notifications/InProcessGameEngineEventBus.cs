using System.Collections.Concurrent;
using System.Threading.Channels;

namespace HexMaster.ThePrey.Games.Notifications;

/// <summary>
/// In-process event bus for game engine location broadcasts. Each game gets an unbounded channel;
/// subscribers receive location events via <see cref="Subscribe"/>. Channels are created lazily
/// and torn down when the game ends or the SSE connection closes.
/// </summary>
public sealed class InProcessGameEngineEventBus : IGameEngineEventBus
{
    private readonly ConcurrentDictionary<Guid, Channel<EngineLocationEvent>> _channels = new();

    public IAsyncEnumerable<EngineLocationEvent> Subscribe(Guid gameId)
    {
        var channel = _channels.GetOrAdd(gameId, _ => Channel.CreateUnbounded<EngineLocationEvent>());
        return channel.Reader.ReadAllAsync();
    }

    public ValueTask PublishAsync(Guid gameId, EngineLocationEvent evt, CancellationToken ct = default)
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
