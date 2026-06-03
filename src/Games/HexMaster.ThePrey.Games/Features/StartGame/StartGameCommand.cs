using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;

namespace HexMaster.ThePrey.Games.Features.StartGame;

public sealed record StartGameCommand(Guid GameId, Guid RequestingUserId, Guid HunterUserId);

public sealed record StartGameResult(GameDto Game);
