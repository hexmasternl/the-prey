namespace HexMaster.ThePrey.IntegrationEvents.Events;

/// <summary>Published when a participant's broadcast ("last known") position is refreshed by the sweep.</summary>
public sealed record PlayerLocationUpdatedIntegrationEvent(
    Guid GameId,
    Guid UserId,
    double Latitude,
    double Longitude,
    string ParticipantState) : IntegrationEvent, IGameScopedIntegrationEvent
{
    public override string Topic => IntegrationEventTopics.PlayerLocationUpdated;
}
