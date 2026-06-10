namespace HexMaster.ThePrey.IntegrationEvents.Events;

/// <summary>Published when a participant's state changes (e.g. Active → Passive → Out).</summary>
public sealed record PlayerStatusChangedIntegrationEvent(
    Guid GameId,
    Guid UserId,
    string Role,
    string NewState) : IntegrationEvent, IGameScopedIntegrationEvent
{
    public override string Topic => IntegrationEventTopics.PlayerStatusChanged;
}
