namespace HexMaster.ThePrey.Games.Notifications;

public interface IGameEventBus
{
    ValueTask PublishAsync(Guid gameId, string eventType, object payload, CancellationToken ct = default);
}
