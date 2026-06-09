namespace HexMaster.ThePrey.Games.Features.CompleteGame;

/// <summary>Marks a game as naturally completed at the scheduled end time.</summary>
public sealed record CompleteGameCommand(Guid GameId);

/// <summary>Returned by the handler. <see cref="AlreadyCompleted"/> is true when the game was already Completed (idempotent).</summary>
public sealed record CompleteGameResult(bool AlreadyCompleted);
