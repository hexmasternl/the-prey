using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

namespace HexMaster.ThePrey.Games.Notifications;

public interface ILobbyEventBus
{
    ValueTask PublishAsync(Guid gameId, string eventType, GameDto payload, CancellationToken ct = default);
}
