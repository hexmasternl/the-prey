namespace HexMaster.ThePrey.Games.Notifications;

public interface ILobbyEventBus
{
    ValueTask PublishAsync(Guid gameId, string eventType, object payload, CancellationToken ct = default);
}
