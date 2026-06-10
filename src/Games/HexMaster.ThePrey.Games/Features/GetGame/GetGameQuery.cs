namespace HexMaster.ThePrey.Games.Features.GetGame;

public sealed record GetGameQuery(Guid GameId, Guid RequestingUserId);
