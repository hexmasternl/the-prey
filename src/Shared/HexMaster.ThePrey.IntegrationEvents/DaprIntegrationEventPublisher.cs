using Dapr.Client;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.IntegrationEvents;

/// <summary>
/// Publishes integration events through Dapr pub/sub. The component named <see cref="PubSubName"/>
/// is backed by RabbitMQ when running under Aspire and by Azure Service Bus in the cloud — the
/// publisher is agnostic to which broker is wired behind the Dapr component.
/// </summary>
public sealed class DaprIntegrationEventPublisher : IIntegrationEventPublisher
{
    /// <summary>
    /// The Dapr pub/sub component name. MUST match the component registered in the Aspire AppHost
    /// (<c>AspireConstants.Resources.DaprPubSub</c>) and the cloud Dapr component.
    /// </summary>
    public const string PubSubName = "pubsub";

    private readonly DaprClient _dapr;
    private readonly ILogger<DaprIntegrationEventPublisher> _logger;

    public DaprIntegrationEventPublisher(DaprClient dapr, ILogger<DaprIntegrationEventPublisher> logger)
    {
        _dapr = dapr;
        _logger = logger;
    }

    public async Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // Cast to object so System.Text.Json serializes the concrete event's properties
        // (publishing as IIntegrationEvent would emit only the interface members).
        await _dapr.PublishEventAsync(PubSubName, integrationEvent.Topic, (object)integrationEvent, ct);

        _logger.LogInformation(
            "Published integration event {EventType} ({EventId}) to topic {Topic}",
            integrationEvent.GetType().Name, integrationEvent.Id, integrationEvent.Topic);
    }
}
