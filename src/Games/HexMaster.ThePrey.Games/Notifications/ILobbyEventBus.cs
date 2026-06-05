using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

namespace HexMaster.ThePrey.Games.Notifications;

public interface ILobbyEventBus
{
    IAsyncEnumerable<LobbyEvent> Subscribe(Guid gameId);
    ValueTask PublishAsync(Guid gameId, string eventType, GameDto payload, CancellationToken ct = default);
    void Complete(Guid gameId);
}
