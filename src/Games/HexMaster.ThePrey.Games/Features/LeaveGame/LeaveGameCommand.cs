namespace HexMaster.ThePrey.Games.Features.LeaveGame;

public sealed record LeaveGameCommand(Guid GameId, Guid UserId);

public sealed record LeaveGameResult;
