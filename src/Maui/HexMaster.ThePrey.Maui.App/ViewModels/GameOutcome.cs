namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>Which side took the game — the lone hunter, or the preys.</summary>
public enum OutcomeSide
{
    Hunter,
    Preys
}

/// <summary>Why the game concluded.</summary>
public enum OutcomeReason
{
    /// <summary>Every prey was tagged before the clock ran out — a hunter win.</summary>
    AllPreysCaught,

    /// <summary>The game duration expired with at least one prey still at large — a preys win.</summary>
    TimeExpired
}

/// <summary>
/// The resolved conclusion of a game as the local player experiences it: whether <em>they</em> won, which
/// side took it, why it ended, and how many preys were still at large. Produced by
/// <see cref="GameOutcomeResolver"/> and projected to text by the outcome view model.
/// </summary>
public sealed record GameOutcome(
    bool LocalPlayerWon,
    OutcomeSide WinningSide,
    OutcomeReason EndReason,
    int SurvivingPreyCount);
