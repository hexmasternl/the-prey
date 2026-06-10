namespace HexMaster.ThePrey.IntegrationEvents.Events;

/// <summary>
/// Carries an in-process game event (its <paramref name="Name"/> and <paramref name="Payload"/>)
/// across the service boundary. The Notifications module forwards it to the game's group as
/// <c>{ type: Name, data: Payload }</c>, preserving the original event shape for clients.
/// </summary>
public sealed record GameNotificationIntegrationEvent(
    Guid GameId,
    string Name,
    object Payload) : IntegrationEvent, IGameScopedIntegrationEvent
{
    public override string Topic => IntegrationEventTopics.GameNotification;
}
