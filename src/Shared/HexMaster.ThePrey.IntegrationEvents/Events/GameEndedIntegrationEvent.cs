namespace HexMaster.ThePrey.IntegrationEvents.Events;

/// <summary>Published when a game completes, carrying the outcome and surviving prey count.</summary>
public sealed record GameEndedIntegrationEvent(
    Guid GameId,
    string Outcome,
    int SurvivorCount) : IntegrationEvent, IGameScopedIntegrationEvent
{
    public override string Topic => IntegrationEventTopics.GameEnded;
}
