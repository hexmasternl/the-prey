using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.ViewModels;

namespace HexMaster.ThePrey.Maui.App.Tests;

/// <summary>
/// The full win/lose matrix: hunter vs. prey × all-caught vs. time-expired × survived vs. caught.
/// </summary>
public class GameOutcomeResolverTests
{
    [Fact]
    public void Resolve_ShouldGiveHunterTheWin_WhenEveryPreyWasCaught()
    {
        var outcome = GameOutcomeResolver.Resolve(isHunter: true, localPlayerCaught: false, survivingPreyCount: 0);

        Assert.True(outcome.LocalPlayerWon);
        Assert.Equal(OutcomeSide.Hunter, outcome.WinningSide);
        Assert.Equal(OutcomeReason.AllPreysCaught, outcome.EndReason);
        Assert.Equal(0, outcome.SurvivingPreyCount);
    }

    [Fact]
    public void Resolve_ShouldGivePreyTheLoss_WhenEveryPreyWasCaught()
    {
        var outcome = GameOutcomeResolver.Resolve(isHunter: false, localPlayerCaught: true, survivingPreyCount: 0);

        Assert.False(outcome.LocalPlayerWon);
        Assert.Equal(OutcomeSide.Hunter, outcome.WinningSide);
        Assert.Equal(OutcomeReason.AllPreysCaught, outcome.EndReason);
    }

    [Fact]
    public void Resolve_ShouldGiveSurvivingPreyTheWin_WhenTimeExpired()
    {
        var outcome = GameOutcomeResolver.Resolve(isHunter: false, localPlayerCaught: false, survivingPreyCount: 2);

        Assert.True(outcome.LocalPlayerWon);
        Assert.Equal(OutcomeSide.Preys, outcome.WinningSide);
        Assert.Equal(OutcomeReason.TimeExpired, outcome.EndReason);
        Assert.Equal(2, outcome.SurvivingPreyCount);
    }

    [Fact]
    public void Resolve_ShouldGiveHunterTheLoss_WhenTimeExpiredWithSurvivors()
    {
        var outcome = GameOutcomeResolver.Resolve(isHunter: true, localPlayerCaught: false, survivingPreyCount: 1);

        Assert.False(outcome.LocalPlayerWon);
        Assert.Equal(OutcomeSide.Preys, outcome.WinningSide);
        Assert.Equal(OutcomeReason.TimeExpired, outcome.EndReason);
    }

    [Fact]
    public void Resolve_ShouldGiveCaughtPreyTheLoss_WhenTheOtherPreysWonOnTime()
    {
        // Only survivors share a time-expiry win — being caught earlier is still a loss.
        var outcome = GameOutcomeResolver.Resolve(isHunter: false, localPlayerCaught: true, survivingPreyCount: 3);

        Assert.False(outcome.LocalPlayerWon);
        Assert.Equal(OutcomeSide.Preys, outcome.WinningSide);
        Assert.Equal(OutcomeReason.TimeExpired, outcome.EndReason);
    }

    [Fact]
    public void Resolve_ShouldCountSurvivorsAndFindLocalPlayer_WhenReadingTheRecord()
    {
        var hunterId = Guid.NewGuid();
        var caughtPreyId = Guid.NewGuid();
        var game = CompletedGame(
            hunterId,
            Participant(hunterId, "Active"),
            Participant(caughtPreyId, "Tagged"),
            Participant(Guid.NewGuid(), "Active"));

        var outcome = GameOutcomeResolver.Resolve(game, localUserId: caughtPreyId, isHunterHint: false);

        Assert.False(outcome.LocalPlayerWon);
        Assert.Equal(OutcomeSide.Preys, outcome.WinningSide);
        Assert.Equal(1, outcome.SurvivingPreyCount);
    }

    [Fact]
    public void Resolve_ShouldTakeRoleFromTheRecord_WhenItContradictsTheHint()
    {
        var hunterId = Guid.NewGuid();
        var game = CompletedGame(
            hunterId,
            Participant(hunterId, "Active"),
            Participant(Guid.NewGuid(), "Tagged"));

        // The caller believed it was a prey; the record says this user is the hunter, and the record wins.
        var outcome = GameOutcomeResolver.Resolve(game, localUserId: hunterId, isHunterHint: false);

        Assert.True(outcome.LocalPlayerWon);
        Assert.Equal(OutcomeSide.Hunter, outcome.WinningSide);
        Assert.Equal(OutcomeReason.AllPreysCaught, outcome.EndReason);
    }

    [Fact]
    public void Resolve_ShouldFallBackToTheHint_WhenTheRecordCarriesNoHunter()
    {
        var game = CompletedGame(hunterUserId: null, Participant(Guid.NewGuid(), "Tagged"));

        var outcome = GameOutcomeResolver.Resolve(game, localUserId: Guid.NewGuid(), isHunterHint: true);

        Assert.True(outcome.LocalPlayerWon);
        Assert.Equal(OutcomeSide.Hunter, outcome.WinningSide);
    }

    private static GameParticipantDetails Participant(Guid userId, string state) =>
        new(userId, "Callsign", IsReady: true, State: state);

    private static GameDetails CompletedGame(Guid? hunterUserId, params GameParticipantDetails[] participants) =>
        new(Guid.NewGuid(), "1234", "Completed",
            new GameConfigurationDetails(30, 5, 10, 120, 60),
            participants,
            hunterUserId, OwnerUserId: Guid.NewGuid(), IsOwnerPlayer: false, IsReadyToStart: false);
}
