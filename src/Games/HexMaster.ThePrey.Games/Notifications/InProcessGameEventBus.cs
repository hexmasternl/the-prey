using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using HexMaster.ThePrey.IntegrationEvents;
using HexMaster.ThePrey.IntegrationEvents.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HexMaster.ThePrey.Games.Notifications;

/// <summary>
/// In-process event bus for gameplay events. Each subscriber gets its own unbounded channel so a
/// published event is broadcast to every connected participant — a single shared channel would make
/// subscribers compete for events, delivering each event to only one of them. Channels are created
/// lazily on subscribe and torn down when the game ends or the SSE connection closes.
///
/// In addition to in-process delivery, every published event is bridged to an integration event so the
/// Notifications module can fan it out to clients over Web PubSub — except <c>participant-located</c>,
/// which is high-frequency (one per GPS post) and is instead broadcast (throttled) by the game sweep.
/// </summary>
public sealed class InProcessGameEventBus : IGameEventBus
{
    private const string LocationEventType = "participant-located";

    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Channel<GameEvent>>> _subscribers = new();
    private readonly IIntegrationEventPublisher _integrationPublisher;
    private readonly ILogger<InProcessGameEventBus> _logger;

    public InProcessGameEventBus(IIntegrationEventPublisher integrationPublisher, ILogger<InProcessGameEventBus>? logger = null)
    {
        _integrationPublisher = integrationPublisher;
        _logger = logger ?? NullLogger<InProcessGameEventBus>.Instance;
    }

    public IAsyncEnumerable<GameEvent> Subscribe(Guid gameId)
    {
        var gameSubscribers = _subscribers.GetOrAdd(gameId, _ => new ConcurrentDictionary<Guid, Channel<GameEvent>>());
        var subscriberId = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<GameEvent>();
        gameSubscribers[subscriberId] = channel;
        _logger.LogInformation(
            "Game subscriber {SubscriberId} added for game {GameId}; total subscribers now {Count}",
            subscriberId, gameId, gameSubscribers.Count);
        return ReadSubscription(gameId, subscriberId, channel.Reader);
    }

    public async ValueTask PublishAsync(Guid gameId, GameEvent evt, CancellationToken ct = default)
    {
        if (_subscribers.TryGetValue(gameId, out var gameSubscribers) && !gameSubscribers.IsEmpty)
        {
            var delivered = 0;
            foreach (var kvp in gameSubscribers)
            {
                if (kvp.Value.Writer.TryWrite(evt))
                    delivered++;
                else
                    RemoveSubscriber(gameId, kvp.Key);
            }

            _logger.LogDebug(
                "Game event '{EventType}' for game {GameId} queued to {Delivered} subscriber(s)",
                evt.EventType, gameId, delivered);
        }

        // Bridge to Web PubSub via an integration event (best-effort). Skip high-frequency location
        // events — the sweep is the throttled mechanism for broadcasting positions.
        if (evt.EventType != LocationEventType)
        {
            try
            {
                await _integrationPublisher.PublishAsync(
                    new GameNotificationIntegrationEvent(gameId, evt.EventType, evt), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to bridge game event '{EventType}' for game {GameId} to Web PubSub.", evt.EventType, gameId);
            }
        }
    }

    public void Complete(Guid gameId)
    {
        if (_subscribers.TryRemove(gameId, out var gameSubscribers))
        {
            foreach (var subscriber in gameSubscribers.Values)
                subscriber.Writer.TryComplete();
        }
    }

    private async IAsyncEnumerable<GameEvent> ReadSubscription(
        Guid gameId,
        Guid subscriberId,
        ChannelReader<GameEvent> reader,
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
            {
                channel.Writer.TryComplete();
                _logger.LogInformation(
                    "Game subscriber {SubscriberId} removed for game {GameId}; {Count} subscriber(s) remain",
                    subscriberId, gameId, gameSubscribers.Count);
            }

            if (gameSubscribers.IsEmpty)
                _subscribers.TryRemove(gameId, out _);
        }
    }
}
