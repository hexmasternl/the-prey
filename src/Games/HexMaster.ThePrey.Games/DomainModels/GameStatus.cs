namespace HexMaster.ThePrey.Games.DomainModels;

/// <summary>The lifecycle state of a game.</summary>
public enum GameStatus
{
    /// <summary>Players are gathering; the game has not started.</summary>
    Lobby = 1,

    /// <summary>The owner has armed the game (roles fixed) but the engine sweep has not yet committed the start; no game clock is running.</summary>
    Ready = 2,

    /// <summary>The game is running.</summary>
    InProgress = 3,

    /// <summary>The game has finished.</summary>
    Completed = 4
}
