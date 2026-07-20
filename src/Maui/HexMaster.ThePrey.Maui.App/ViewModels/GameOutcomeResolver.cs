using HexMaster.ThePrey.Maui.App.Services.Api;

namespace HexMaster.ThePrey.Maui.App.ViewModels;

/// <summary>
/// The pure win/lose rule of a finished game. Free of HTTP, MAUI and the clock, so the whole matrix
/// (hunter vs. prey × all-caught vs. time-up × survived vs. caught) is exhaustively unit-testable.
/// </summary>
/// <remarks>
/// The rule has exactly two branches, both driven by the surviving-prey count:
/// <list type="bullet">
/// <item>No prey survived → the hunter caught everyone: the hunter wins, every prey loses.</item>
/// <item>At least one prey survived → the clock ran out: the <em>surviving</em> preys win, the hunter
/// loses, and a prey who had already been caught loses too (only survivors share the win).</item>
/// </list>
/// </remarks>
public static class GameOutcomeResolver
{
    /// <summary>
    /// Resolves the outcome for the local player. <paramref name="localPlayerCaught"/> is ignored for the
    /// hunter (whose fate is the catch-them-all result) and decides a prey's share of a time-expiry win.
    /// </summary>
    public static GameOutcome Resolve(bool isHunter, bool localPlayerCaught, int survivingPreyCount)
    {
        if (survivingPreyCount < 0)
            survivingPreyCount = 0;

        var allCaught = survivingPreyCount == 0;
        var reason = allCaught ? OutcomeReason.AllPreysCaught : OutcomeReason.TimeExpired;
        var winningSide = allCaught ? OutcomeSide.Hunter : OutcomeSide.Preys;

        var localPlayerWon = isHunter
            ? allCaught
            : !allCaught && !localPlayerCaught;

        return new GameOutcome(localPlayerWon, winningSide, reason, survivingPreyCount);
    }

    /// <summary>
    /// Resolves the outcome straight from a completed game record: derives each prey's survived/caught
    /// fate from its participant <c>State</c> (Tagged/Out = caught), counts the survivors, and locates the
    /// local player. The role is taken from the record's <c>HunterUserId</c> when it identifies
    /// <paramref name="localUserId"/>, falling back to <paramref name="isHunterHint"/> (what the caller
    /// believed at hand-off) when the record carries no hunter.
    /// </summary>
    public static GameOutcome Resolve(GameDetails game, Guid? localUserId, bool isHunterHint)
    {
        var isHunter = game.HunterUserId is { } hunterUserId && localUserId is { } me
            ? me == hunterUserId
            : isHunterHint;

        var survivingPreyCount = 0;
        var localPlayerCaught = false;

        foreach (var participant in game.Participants)
        {
            var isThisTheHunter = game.HunterUserId is { } hunter && participant.UserId == hunter;
            var caught = GameMapProjection.IsCaught(participant.State);

            if (!isThisTheHunter && !caught)
                survivingPreyCount++;

            if (localUserId is { } self && participant.UserId == self)
                localPlayerCaught = caught;
        }

        return Resolve(isHunter, localPlayerCaught, survivingPreyCount);
    }
}
