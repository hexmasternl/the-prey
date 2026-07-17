namespace HexMaster.ThePrey.Games.Notifications;

public interface IGameEventBus
{
    ValueTask PublishAsync(Guid gameId, GameEvent evt, CancellationToken ct = default);
}
