using HexMaster.ThePrey.IntegrationEvents;
using HexMaster.ThePrey.IntegrationEvents.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HexMaster.ThePrey.Games.Notifications;

/// <summary>
/// Bridges in-process gameplay events to an integration event so the Notifications module can fan
/// them out to clients over Web PubSub.
/// </summary>
public sealed class InProcessGameEventBus : IGameEventBus
{
    private readonly IIntegrationEventPublisher _integrationPublisher;
    private readonly ILogger<InProcessGameEventBus> _logger;

    public InProcessGameEventBus(IIntegrationEventPublisher integrationPublisher, ILogger<InProcessGameEventBus>? logger = null)
    {
        _integrationPublisher = integrationPublisher;
        _logger = logger ?? NullLogger<InProcessGameEventBus>.Instance;
    }

    public async ValueTask PublishAsync(Guid gameId, string eventType, object payload, CancellationToken ct = default)
    {
        try
        {
            await _integrationPublisher.PublishAsync(
                new GameNotificationIntegrationEvent(gameId, eventType, payload), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to bridge game event '{EventType}' for game {GameId} to Web PubSub.", eventType, gameId);
        }
    }
}
