namespace HexMaster.ThePrey.Games.Notifications;

public interface IGameEventBus
{
    IAsyncEnumerable<GameEvent> Subscribe(Guid gameId);
    ValueTask PublishAsync(Guid gameId, GameEvent evt, CancellationToken ct = default);
    void Complete(Guid gameId);
}
