namespace HexMaster.ThePrey.Games.DomainModels;

/// <summary>The lifecycle state of a game.</summary>
public enum GameStatus
{
    /// <summary>Players are gathering; the game has not started.</summary>
    Lobby = 1,

    /// <summary>Every non-owner participant has readied up and the game may be started; the owner's start action is enabled. No roles are fixed and no clock is running.</summary>
    Ready = 2,

    /// <summary>The game is running.</summary>
    InProgress = 3,

    /// <summary>The game has finished.</summary>
    Completed = 4,

    /// <summary>
    /// The owner has committed to starting (roles fixed) but the engine sweep has not yet committed the start;
    /// no game clock is running. This is the players' intent to start and the signal for clients to navigate to
    /// the gameplay page. Numeric value is intentionally out of lifecycle order (the status is persisted and
    /// serialized by name, so the ordinal is irrelevant) to keep the existing stored ordinals stable.
    /// </summary>
    Started = 5
}
