using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Tests.Factories;

namespace HexMaster.ThePrey.Games.Tests.DomainModels;

public sealed class PlayerStateTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);

    // ── 1.4 ─ creation defaults ────────────────────────────────────────────

    [Fact]
    public void StartedGame_AllParticipants_DefaultToActiveState()
    {
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start);

        var hunter = game.Participants.Single(p => p.UserId == hunterId);
        Assert.Equal(PlayerState.Active, hunter.State);
        foreach (var id in preyIds)
            Assert.Equal(PlayerState.Active, game.Participants.Single(p => p.UserId == id).State);
    }

    [Fact]
    public void StartedGame_AllParticipants_HaveNullLastLocationAt()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start);

        foreach (var id in preyIds)
            Assert.Null(game.Participants.Single(p => p.UserId == id).LastLocationAt);
    }

    // ── 2.x ─ RecordLocation state transitions ─────────────────────────────

    [Fact]
    public void RecordLocation_ShouldActivateParticipant_AndReturnPreviousState()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start);
        var preyId = preyIds[0];
        var coord = GpsCoordinate.Create(52.1, 5.1);

        var outcome = game.RecordLocation(preyId, coord, Start.AddMinutes(1));

        Assert.Equal(PlayerState.Active, outcome.PreviousState);
        Assert.Equal(PlayerState.Active, game.Participants.Single(p => p.UserId == preyId).State);
        Assert.Equal(Start.AddMinutes(1), game.Participants.Single(p => p.UserId == preyId).LastLocationAt);
    }

    [Fact]
    public void RecordLocation_WhenParticipantWasPassive_ShouldReturnPassivePreviousState()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start);
        var preyId = preyIds[0];
        var coord = GpsCoordinate.Create(52.1, 5.1);

        // First record: sets Active + LastLocationAt
        game.RecordLocation(preyId, coord, Start);
        // Manually advance time to 6 min (beyond 5-min passive threshold) via timeout transition
        game.ApplyTimeoutTransitions(Start.AddMinutes(6));
        Assert.Equal(PlayerState.Passive, game.Participants.Single(p => p.UserId == preyId).State);

        // Record again: should return Passive (previous) and set Active
        var outcome = game.RecordLocation(preyId, coord, Start.AddMinutes(7));
        Assert.Equal(PlayerState.Passive, outcome.PreviousState);
        Assert.Equal(PlayerState.Active, game.Participants.Single(p => p.UserId == preyId).State);
    }

    [Fact]
    public void RecordLocation_WhenParticipantIsOut_ShouldNotChangeState()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start);
        var preyId = preyIds[0];
        var coord = GpsCoordinate.Create(52.1, 5.1);

        game.RecordLocation(preyId, coord, Start);
        // Advance 8 min → Out
        game.ApplyTimeoutTransitions(Start.AddMinutes(8));
        Assert.Equal(PlayerState.Out, game.Participants.Single(p => p.UserId == preyId).State);

        // Record should be a no-op on state
        var outcome = game.RecordLocation(preyId, coord, Start.AddMinutes(9));
        Assert.Equal(PlayerState.Out, outcome.PreviousState);
        Assert.Equal(PlayerState.Out, game.Participants.Single(p => p.UserId == preyId).State);
    }

    [Fact]
    public void RecordLocation_WhenParticipantIsTagged_ShouldNotChangeState()
    {
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start);
        var preyId = preyIds[0];
        var coord = GpsCoordinate.Create(52.1, 5.1);

        game.RecordLocation(preyId, coord, Start);
        game.TagParticipant(hunterId, preyId, Start.AddMinutes(10));
        Assert.Equal(PlayerState.Tagged, game.Participants.Single(p => p.UserId == preyId).State);

        var outcome = game.RecordLocation(preyId, coord, Start.AddMinutes(1));
        Assert.Equal(PlayerState.Tagged, outcome.PreviousState);
        Assert.Equal(PlayerState.Tagged, game.Participants.Single(p => p.UserId == preyId).State);
    }

    // ── 3.x ─ ApplyTimeoutTransitions ─────────────────────────────────────

    [Fact]
    public void ApplyTimeoutTransitions_ShouldTransitionToPassive_AtFiveMinutes()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start);
        var preyId = preyIds[0];
        game.RecordLocation(preyId, GpsCoordinate.Create(52.1, 5.1), Start);

        var changes = game.ApplyTimeoutTransitions(Start.AddMinutes(5));

        Assert.Single(changes);
        Assert.Equal(preyId, changes[0].UserId);
        Assert.Equal(PlayerState.Passive, changes[0].NewState);
    }

    [Fact]
    public void ApplyTimeoutTransitions_ShouldTransitionToOut_AtSevenMinutes()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start);
        var preyId = preyIds[0];
        game.RecordLocation(preyId, GpsCoordinate.Create(52.1, 5.1), Start);

        var changes = game.ApplyTimeoutTransitions(Start.AddMinutes(7));

        Assert.Single(changes);
        Assert.Equal(preyId, changes[0].UserId);
        Assert.Equal(PlayerState.Out, changes[0].NewState);
    }

    [Fact]
    public void ApplyTimeoutTransitions_ShouldNotTransitionOut_WhenAlreadyOut()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start);
        var preyId = preyIds[0];
        game.RecordLocation(preyId, GpsCoordinate.Create(52.1, 5.1), Start);

        game.ApplyTimeoutTransitions(Start.AddMinutes(8)); // transitions to Out
        var changes = game.ApplyTimeoutTransitions(Start.AddMinutes(15)); // should be empty

        Assert.Empty(changes);
    }

    [Fact]
    public void ApplyTimeoutTransitions_ShouldNotTransitionTagged_ToOut()
    {
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start);
        var preyId = preyIds[0];
        game.RecordLocation(preyId, GpsCoordinate.Create(52.1, 5.1), Start);
        game.TagParticipant(hunterId, preyId, Start.AddMinutes(10));

        var changes = game.ApplyTimeoutTransitions(Start.AddMinutes(10));

        Assert.Empty(changes);
        Assert.Equal(PlayerState.Tagged, game.Participants.Single(p => p.UserId == preyId).State);
    }

    [Fact]
    public void ApplyTimeoutTransitions_ShouldNotTransition_WhenNullLastLocationAt()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start);
        // Preys have no location recorded yet

        var changes = game.ApplyTimeoutTransitions(Start.AddMinutes(10));

        Assert.Empty(changes);
    }

    // ── 4.x ─ TagParticipant ───────────────────────────────────────────────

    [Fact]
    public void TagParticipant_ShouldSetTaggedState_WhenCallerIsHunterAndTargetIsActive()
    {
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start);

        game.TagParticipant(hunterId, preyIds[0], Start.AddMinutes(10));

        Assert.Equal(PlayerState.Tagged, game.Participants.Single(p => p.UserId == preyIds[0]).State);
    }

    [Fact]
    public void TagParticipant_ShouldSetTaggedState_WhenTargetIsPassive()
    {
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start);
        var preyId = preyIds[0];
        game.RecordLocation(preyId, GpsCoordinate.Create(52.1, 5.1), Start);
        game.ApplyTimeoutTransitions(Start.AddMinutes(6));
        Assert.Equal(PlayerState.Passive, game.Participants.Single(p => p.UserId == preyId).State);

        game.TagParticipant(hunterId, preyId, Start.AddMinutes(10));

        Assert.Equal(PlayerState.Tagged, game.Participants.Single(p => p.UserId == preyId).State);
    }

    [Fact]
    public void TagParticipant_ShouldThrow_WhenCallerIsNotHunter()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start);

        Assert.Throws<UnauthorizedAccessException>(() =>
            game.TagParticipant(preyIds[0], preyIds[1], Start.AddMinutes(10)));
    }

    [Fact]
    public void TagParticipant_ShouldThrow_WhenTargetIsAlreadyTagged()
    {
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start);
        game.TagParticipant(hunterId, preyIds[0], Start.AddMinutes(10));

        Assert.Throws<InvalidOperationException>(() =>
            game.TagParticipant(hunterId, preyIds[0], Start.AddMinutes(10)));
    }

    [Fact]
    public void TagParticipant_ShouldThrow_WhenTargetIsOut()
    {
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start);
        var preyId = preyIds[0];
        game.RecordLocation(preyId, GpsCoordinate.Create(52.1, 5.1), Start);
        game.ApplyTimeoutTransitions(Start.AddMinutes(8));

        Assert.Throws<InvalidOperationException>(() =>
            game.TagParticipant(hunterId, preyId, Start.AddMinutes(10)));
    }
}
