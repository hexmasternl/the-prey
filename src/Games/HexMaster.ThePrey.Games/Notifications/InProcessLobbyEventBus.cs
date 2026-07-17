using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.IntegrationEvents;
using HexMaster.ThePrey.IntegrationEvents.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HexMaster.ThePrey.Games.Notifications;

/// <summary>
/// Bridges in-process lobby state changes to an integration event so the Notifications module can
/// fan them out to lobby clients over Web PubSub.
/// </summary>
public sealed class InProcessLobbyEventBus : ILobbyEventBus
{
    private readonly IIntegrationEventPublisher _integrationPublisher;
    private readonly ILogger<InProcessLobbyEventBus> _logger;

    public InProcessLobbyEventBus(IIntegrationEventPublisher integrationPublisher, ILogger<InProcessLobbyEventBus>? logger = null)
    {
        _integrationPublisher = integrationPublisher;
        _logger = logger ?? NullLogger<InProcessLobbyEventBus>.Instance;
    }

    public async ValueTask PublishAsync(Guid gameId, string eventType, GameDto payload, CancellationToken ct = default)
    {
        try
        {
            await _integrationPublisher.PublishAsync(
                new LobbyNotificationIntegrationEvent(gameId, eventType, payload), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to bridge lobby event '{EventType}' for game {GameId} to Web PubSub.", eventType, gameId);
        }
    }
}
