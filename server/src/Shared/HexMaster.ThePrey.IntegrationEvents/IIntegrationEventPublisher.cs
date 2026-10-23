namespace HexMaster.ThePrey.IntegrationEvents;

/// <summary>Publishes integration events to the configured pub/sub broker.</summary>
public interface IIntegrationEventPublisher
{
    Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken ct = default);
}
