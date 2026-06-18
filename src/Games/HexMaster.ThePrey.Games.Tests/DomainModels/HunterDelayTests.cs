using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Tests.Factories;

namespace HexMaster.ThePrey.Games.Tests.DomainModels;

public sealed class HunterDelayTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);

    private static readonly GpsCoordinate Anchor = GpsCoordinate.Create(52.1, 5.1);
    // ~111 m north of the anchor: beyond the 25 m threshold.
    private static readonly GpsCoordinate BeyondThreshold = GpsCoordinate.Create(52.101, 5.1);
    // ~30 m north of the anchor: beyond the 25 m threshold (comfortably over).
    private static readonly GpsCoordinate JustBeyondThreshold = GpsCoordinate.Create(52.10027, 5.1);
    // ~22 m north of the anchor: within the 25 m threshold.
    private static readonly GpsCoordinate WithinThreshold = GpsCoordinate.Create(52.1002, 5.1);

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
    public void AssessHunterHeadStartPenalty_ShouldAnchorFirstHunterLocation_WhenDuringDelay()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, Start);
        game.RecordLocation(hunterId, Anchor, Start.AddMinutes(1));

        game.AssessHunterHeadStartPenalty(Start.AddMinutes(1));

        var hunter = game.Participants.Single(p => p.UserId == hunterId);
        Assert.Equal(Anchor, hunter.DelayAnchorLocation);
    }

    [Fact]
    public void RecordLocation_ShouldNotMoveAnchor_OnLaterReportsDuringDelay()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, Start);

        game.RecordLocation(hunterId, Anchor, Start.AddMinutes(1));
        game.RecordLocation(hunterId, WithinThreshold, Start.AddMinutes(2));
        game.AssessHunterHeadStartPenalty(Start.AddMinutes(2));

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

    // ── Movement penalty ────────────────────────────────────────────────────

    [Fact]
    public void AssessHunterHeadStartPenalty_ShouldApplyPenaltyEndingTenMinutesAfterDelay_WhenHunterMovesBeyondThreshold()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, Start);
        game.RecordLocation(hunterId, Anchor, Start.AddMinutes(1));
        game.RecordLocation(hunterId, BeyondThreshold, Start.AddMinutes(2));

        var penalty = game.AssessHunterHeadStartPenalty(Start.AddMinutes(2));

        Assert.NotNull(penalty);
        Assert.Equal(Start.AddMinutes(5 + Game.HunterDelayPenaltyMinutes), penalty!.EndsAt);
        var hunter = game.Participants.Single(p => p.UserId == hunterId);
        Assert.True(hunter.HasActivePenalty(Start.AddMinutes(2)));
    }

    [Fact]
    public void AssessHunterHeadStartPenalty_ShouldApplyPenalty_WhenHunterMovesJustBeyond25Meters()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, Start);
        game.RecordLocation(hunterId, Anchor, Start.AddMinutes(1));
        game.RecordLocation(hunterId, JustBeyondThreshold, Start.AddMinutes(2));

        var penalty = game.AssessHunterHeadStartPenalty(Start.AddMinutes(2));

        Assert.NotNull(penalty);
    }

    [Fact]
    public void AssessHunterHeadStartPenalty_ShouldNotApplyPenalty_WhenHunterStaysWithin25Meters()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, Start);
        game.RecordLocation(hunterId, Anchor, Start.AddMinutes(1));
        game.RecordLocation(hunterId, WithinThreshold, Start.AddMinutes(2));

        var penalty = game.AssessHunterHeadStartPenalty(Start.AddMinutes(2));

        Assert.Null(penalty);
        var hunter = game.Participants.Single(p => p.UserId == hunterId);
        Assert.False(hunter.HasActivePenalty(Start.AddMinutes(2)));
    }

    [Fact]
    public void AssessHunterHeadStartPenalty_ShouldApplyAtMostOnePenalty_OnRepeatedAssessments()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, Start);
        game.RecordLocation(hunterId, Anchor, Start.AddMinutes(1));
        game.RecordLocation(hunterId, BeyondThreshold, Start.AddMinutes(2));

        var first = game.AssessHunterHeadStartPenalty(Start.AddMinutes(2));
        var second = game.AssessHunterHeadStartPenalty(Start.AddMinutes(3));
        var third = game.AssessHunterHeadStartPenalty(Start.AddMinutes(4));

        Assert.NotNull(first);
        Assert.Null(second);
        Assert.Null(third);
        var hunter = game.Participants.Single(p => p.UserId == hunterId);
        Assert.Single(hunter.Penalties);
    }

    [Fact]
    public void AssessHunterHeadStartPenalty_ShouldNotApplyPenalty_WhenReadingsAreAllAfterDelay()
    {
        // Readings timestamped >= HunterMayMoveAt must NOT trigger the head-start penalty.
        var game = GameFaker.StartedGame(out var hunterId, out _, Start);
        var mayMoveAt = game.HunterMayMoveAt!.Value; // Start + 5 min
        game.RecordLocation(hunterId, Anchor, mayMoveAt);             // exactly at boundary — not < mayMoveAt
        game.RecordLocation(hunterId, BeyondThreshold, mayMoveAt.AddSeconds(1));

        var penalty = game.AssessHunterHeadStartPenalty(mayMoveAt.AddSeconds(1));

        Assert.Null(penalty);
        var hunter = game.Participants.Single(p => p.UserId == hunterId);
        Assert.Empty(hunter.Penalties);
    }

    [Fact]
    public void AssessHunterHeadStartPenalty_ShouldApplyPenalty_WhenAssessedAfterDelayButReadingsAreBefore()
    {
        // Critical correctness case: a reading emitted during the head-start is assessed after
        // HunterMayMoveAt (the sweep that picks it up runs at/after that time). The gate must use
        // RecordedAt, not the 'now' passed to AssessHunterHeadStartPenalty.
        var game = GameFaker.StartedGame(out var hunterId, out _, Start);
        var mayMoveAt = game.HunterMayMoveAt!.Value; // Start + 5 min

        // Reading timestamped before the delay boundary
        game.RecordLocation(hunterId, Anchor, Start.AddMinutes(1));
        game.RecordLocation(hunterId, BeyondThreshold, mayMoveAt.AddSeconds(-1));

        // Assessed after the delay has expired (simulates a late sweep)
        var penalty = game.AssessHunterHeadStartPenalty(mayMoveAt.AddSeconds(30));

        Assert.NotNull(penalty);
        Assert.Equal(mayMoveAt.AddMinutes(Game.HunterDelayPenaltyMinutes), penalty!.EndsAt);
    }

    [Fact]
    public void AssessHunterHeadStartPenalty_ShouldReturnNull_WhenNoHeadStartReadingsExist()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, Start);
        // Only readings after the delay
        var mayMoveAt = game.HunterMayMoveAt!.Value;
        game.RecordLocation(hunterId, BeyondThreshold, mayMoveAt.AddMinutes(1));

        var penalty = game.AssessHunterHeadStartPenalty(mayMoveAt.AddMinutes(1));

        Assert.Null(penalty);
    }

    [Fact]
    public void AssessHunterHeadStartPenalty_ShouldReturnNull_WhenGameIsNotInProgress()
    {
        var game = GameFaker.LobbyGame();

        var penalty = game.AssessHunterHeadStartPenalty(DateTimeOffset.UtcNow);

        Assert.Null(penalty);
    }

    [Fact]
    public void AssessHunterHeadStartPenalty_ShouldStillRecordReading_WhenPenalized()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, Start);
        game.RecordLocation(hunterId, Anchor, Start.AddMinutes(1));
        game.RecordLocation(hunterId, BeyondThreshold, Start.AddMinutes(2));

        game.AssessHunterHeadStartPenalty(Start.AddMinutes(2));

        var hunter = game.Participants.Single(p => p.UserId == hunterId);
        Assert.Equal(2, hunter.Locations.Count);
        Assert.Equal(BeyondThreshold, hunter.Locations[^1].Coordinate);
        Assert.Equal(Start.AddMinutes(2), hunter.LastLocationAt);
    }
}
