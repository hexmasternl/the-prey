namespace HexMaster.ThePrey.Games.Notifications;

public interface IGameEngineEventBus
{
    IAsyncEnumerable<EngineLocationEvent> Subscribe(Guid gameId);
    ValueTask PublishAsync(Guid gameId, EngineLocationEvent evt, CancellationToken ct = default);
    void Complete(Guid gameId);
}
