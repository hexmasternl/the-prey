namespace HexMaster.ThePrey.IntegrationEvents;

/// <summary>
/// Marker contract for every cross-module integration event. Events are published to a Dapr
/// pub/sub broker (RabbitMQ locally, Azure Service Bus in the cloud) and consumed by the
/// Notifications module. Each event declares the broker <see cref="Topic"/> it is delivered on.
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>Unique identifier of this event occurrence.</summary>
    Guid Id { get; }

    /// <summary>The moment the event occurred (UTC).</summary>
    DateTimeOffset OccurredAt { get; }

    /// <summary>The pub/sub topic this event is published to / consumed from.</summary>
    string Topic { get; }
}
