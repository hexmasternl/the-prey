namespace HexMaster.ThePrey.IntegrationEvents.Events;

/// <summary>Published when a participant is penalised (e.g. for leaving the playfield boundary).</summary>
public sealed record PlayerPenalizedIntegrationEvent(
    Guid GameId,
    Guid UserId,
    DateTimeOffset PenaltyEndsAt,
    string Reason) : IntegrationEvent, IGameScopedIntegrationEvent
{
    public override string Topic => IntegrationEventTopics.PlayerPenalized;
}
