namespace HexMaster.ThePrey.Games.Features.EndGame;

public sealed record EndGameCommand(Guid GameId, Guid RequestingUserId);

public sealed record EndGameResult;
