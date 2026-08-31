namespace HexMaster.ThePrey.Games.Features.GetFullGameState;

/// <summary>Reads the complete state of <paramref name="GameId"/> from <paramref name="UserId"/>'s perspective.</summary>
public sealed record GetFullGameStateQuery(Guid GameId, Guid UserId);
