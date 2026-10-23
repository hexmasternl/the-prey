namespace HexMaster.ThePrey.Games.DomainModels;

/// <summary>The outcome of a completed game.</summary>
public enum GameOutcome
{
    /// <summary>The outcome has not been determined yet, or the game was completed in an unexpected state.</summary>
    Undecided = 0,

    /// <summary>All preys were tagged or knocked out — the hunter wins.</summary>
    HuntersWin = 1,

    /// <summary>At least one prey survived to the end — the preys win.</summary>
    PreysWin = 2,

    /// <summary>The game was cancelled before it started (ended from Lobby state).</summary>
    Cancelled = 3
}
