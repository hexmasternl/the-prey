namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>
/// The four visual phases of a gameplay page, derived from the game's <c>Status</c> and
/// <c>HunterMayMoveAt</c>. Shared by the hunter and prey game view models.
/// </summary>
public enum GamePhase
{
    /// <summary>Game is armed (<c>Started</c>) but the backend sweep has not committed it — waiting overlay.</summary>
    Waiting,

    /// <summary>Game is <c>InProgress</c> and the hunter's head-start countdown is still running.</summary>
    HeadStart,

    /// <summary>Game is <c>InProgress</c> and the head-start has elapsed — the full live map.</summary>
    Live,

    /// <summary>Game is <c>Completed</c> — hand off to the outcome screen (once).</summary>
    Ended
}
