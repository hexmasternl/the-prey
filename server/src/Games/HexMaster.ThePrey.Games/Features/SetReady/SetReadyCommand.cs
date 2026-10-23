using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

namespace HexMaster.ThePrey.Games.Features.SetReady;

public sealed record SetReadyCommand(Guid GameId, Guid UserId);

public sealed record SetReadyResult(GameDto Game);
