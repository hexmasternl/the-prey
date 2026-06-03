namespace HexMaster.ThePrey.Games.DomainModels;

/// <summary>The lifecycle state of a game.</summary>
public enum GameStatus
{
    /// <summary>Players are gathering; the game has not started.</summary>
    Lobby = 1,

    /// <summary>The game is running.</summary>
    InProgress = 2,

    /// <summary>The game has finished.</summary>
    Completed = 3
}
