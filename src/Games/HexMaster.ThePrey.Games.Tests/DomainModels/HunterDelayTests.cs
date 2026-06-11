using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Tests.Factories;

namespace HexMaster.ThePrey.Games.Tests.DomainModels;

public sealed class HunterDelayTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);

    private static readonly GpsCoordinate Anchor = GpsCoordinate.Create(52.1, 5.1);
    // ~111 m north of the anchor: beyond the 50 m threshold.
    private static readonly GpsCoordinate BeyondThreshold = GpsCoordinate.Create(52.101, 5.1);
    // ~11 m north of the anchor: within the 50 m threshold.
    private static readonly GpsCoordinate WithinThreshold = GpsCoordinate.Create(52.1001, 5.1);

    // ── HunterMayMoveAt ─────────────────────────────────────────────────────

    [Fact]
    public void HunterMayMoveAt_ShouldBeStartPlusDelay_WhenGameIsStarted()
    {
        var game = GameFaker.StartedGame(out _, out _, Start,
            configuration: GameFaker.ValidConfiguration(hunterDelayTime: 5));

        Assert.Equal(Start.AddMinutes(5), game.HunterMayMoveAt);
    }

    [Fact]
    public void HunterMayMoveAt_ShouldBeNull_WhenGameHasNotStarted()
    {
        var game = GameFaker.LobbyGame();

        Assert.Null(game.HunterMayMoveAt);
    }

    // ── Anchor behaviour ────────────────────────────────────────────────────

    [Fact]
    public void RecordLocation_ShouldAnchorFirstHunterLocation_WhenDuringDelay()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, Start);

        game.RecordLocation(hunterId, Anchor, Start.AddMinutes(1));

        var hunter = game.Participants.Single(p => p.UserId == hunterId);
        Assert.Equal(Anchor, hunter.DelayAnchorLocation);
    }

    [Fact]
    public void RecordLocation_ShouldNotMoveAnchor_OnLaterReportsDuringDelay()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, Start);

        game.RecordLocation(hunterId, Anchor, Start.AddMinutes(1));
        game.RecordLocation(hunterId, WithinThreshold, Start.AddMinutes(2));

        var hunter = game.Participants.Single(p => p.UserId == hunterId);
        Assert.Equal(Anchor, hunter.DelayAnchorLocation);
    }

    [Fact]
    public void RecordLocation_ShouldNotAnchor_WhenPreyReportsDuringDelay()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start);

        game.RecordLocation(preyIds[0], Anchor, Start.AddMinutes(1));

        var prey = game.Participants.Single(p => p.UserId == preyIds[0]);
        Assert.Null(prey.DelayAnchorLocation);
    }

    [Fact]
    public void RecordLocation_ShouldNotAnchor_WhenHunterReportsAfterDelay()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, Start);

        game.RecordLocation(hunterId, Anchor, Start.AddMinutes(6));

        var hunter = game.Participants.Single(p => p.UserId == hunterId);
        Assert.Null(hunter.DelayAnchorLocation);
    }

    // ── Movement penalty ────────────────────────────────────────────────────

    [Fact]
    public void RecordLocation_ShouldApplyPenaltyEndingTenMinutesAfterDelay_WhenHunterMovesBeyondThreshold()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, Start);

        game.RecordLocation(hunterId, Anchor, Start.AddMinutes(1));
        var outcome = game.RecordLocation(hunterId, BeyondThreshold, Start.AddMinutes(2));

        Assert.NotNull(outcome.DelayViolationPenalty);
        Assert.Equal(Start.AddMinutes(5 + Game.HunterDelayPenaltyMinutes), outcome.DelayViolationPenalty!.EndsAt);
        var hunter = game.Participants.Single(p => p.UserId == hunterId);
        Assert.True(hunter.HasActivePenalty(Start.AddMinutes(2)));
    }

    [Fact]
    public void RecordLocation_ShouldNotApplyPenalty_WhenHunterStaysWithinThreshold()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, Start);

        game.RecordLocation(hunterId, Anchor, Start.AddMinutes(1));
        var outcome = game.RecordLocation(hunterId, WithinThreshold, Start.AddMinutes(2));

        Assert.Null(outcome.DelayViolationPenalty);
        var hunter = game.Participants.Single(p => p.UserId == hunterId);
        Assert.False(hunter.HasActivePenalty(Start.AddMinutes(2)));
    }

    [Fact]
    public void RecordLocation_ShouldNotStackPenalties_OnRepeatedMovementDuringDelay()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, Start);

        game.RecordLocation(hunterId, Anchor, Start.AddMinutes(1));
        var first = game.RecordLocation(hunterId, BeyondThreshold, Start.AddMinutes(2));
        var second = game.RecordLocation(hunterId, BeyondThreshold, Start.AddMinutes(3));

        Assert.NotNull(first.DelayViolationPenalty);
        Assert.Null(second.DelayViolationPenalty);
        var hunter = game.Participants.Single(p => p.UserId == hunterId);
        Assert.Single(hunter.Penalties);
    }

    [Fact]
    public void RecordLocation_ShouldNotApplyPenalty_WhenHunterMovesAfterDelay()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, Start);

        game.RecordLocation(hunterId, Anchor, Start.AddMinutes(1));
        var outcome = game.RecordLocation(hunterId, BeyondThreshold, Start.AddMinutes(5));

        Assert.Null(outcome.DelayViolationPenalty);
        var hunter = game.Participants.Single(p => p.UserId == hunterId);
        Assert.Empty(hunter.Penalties);
    }

    [Fact]
    public void RecordLocation_ShouldStillRecordReading_WhenPenalized()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, Start);

        game.RecordLocation(hunterId, Anchor, Start.AddMinutes(1));
        game.RecordLocation(hunterId, BeyondThreshold, Start.AddMinutes(2));

        var hunter = game.Participants.Single(p => p.UserId == hunterId);
        Assert.Equal(2, hunter.Locations.Count);
        Assert.Equal(BeyondThreshold, hunter.Locations[^1].Coordinate);
        Assert.Equal(Start.AddMinutes(2), hunter.LastLocationAt);
    }
}
