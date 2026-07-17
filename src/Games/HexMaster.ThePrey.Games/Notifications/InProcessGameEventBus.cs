using HexMaster.ThePrey.IntegrationEvents;
using HexMaster.ThePrey.IntegrationEvents.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HexMaster.ThePrey.Games.Notifications;

/// <summary>
/// Bridges in-process gameplay events to an integration event so the Notifications module can fan
/// them out to clients over Web PubSub — except <c>participant-located</c>, which is high-frequency
/// (one per GPS post) and is instead broadcast (throttled) by the game sweep.
/// </summary>
public sealed class InProcessGameEventBus : IGameEventBus
{
    private const string LocationEventType = "participant-located";

    private readonly IIntegrationEventPublisher _integrationPublisher;
    private readonly ILogger<InProcessGameEventBus> _logger;

    public InProcessGameEventBus(IIntegrationEventPublisher integrationPublisher, ILogger<InProcessGameEventBus>? logger = null)
    {
        _integrationPublisher = integrationPublisher;
        _logger = logger ?? NullLogger<InProcessGameEventBus>.Instance;
    }

    public async ValueTask PublishAsync(Guid gameId, GameEvent evt, CancellationToken ct = default)
    {
        // Skip high-frequency location events — the sweep is the throttled mechanism for
        // broadcasting positions.
        if (evt.EventType == LocationEventType)
            return;

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
