namespace HexMaster.ThePrey.IntegrationEvents.Events;

/// <summary>
/// Carries an in-process lobby event (its <paramref name="Name"/> and the updated game
/// <paramref name="Payload"/>) across the service boundary. The Notifications module forwards it to
/// the game's group as <c>{ type: Name, data: Payload }</c> for clients in the lobby.
/// </summary>
public sealed record LobbyNotificationIntegrationEvent(
    Guid GameId,
    string Name,
    object Payload) : IntegrationEvent, IGameScopedIntegrationEvent
{
    public override string Topic => IntegrationEventTopics.LobbyNotification;
}
