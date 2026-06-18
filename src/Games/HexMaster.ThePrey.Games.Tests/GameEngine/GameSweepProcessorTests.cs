using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.GameEngine;
using HexMaster.ThePrey.Games.Observability;
using HexMaster.ThePrey.Games.Tests.Factories;
using HexMaster.ThePrey.IntegrationEvents;
using HexMaster.ThePrey.IntegrationEvents.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.GameEngine;

public sealed class GameSweepProcessorTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 3, 12, 0, 0, TimeSpan.Zero);

    // 2x2 square around the origin (lat/lng). Inside: (0,0). Outside: (5,5).
    private static readonly IReadOnlyList<GpsCoordinate> Square =
    [
        GpsCoordinate.Create(1, 1),
        GpsCoordinate.Create(1, -1),
        GpsCoordinate.Create(-1, -1),
        GpsCoordinate.Create(-1, 1),
    ];

    private readonly Mock<IGameRepository> _games = new();
    private readonly Mock<IPlayfieldBoundaryProvider> _boundary = new();
    private readonly Mock<IIntegrationEventPublisher> _publisher = new();
    private readonly Mock<IGameMetrics> _metrics = new();
    private readonly GameSweepProcessor _sut;

    public GameSweepProcessorTests()
    {
        _boundary.Setup(b => b.GetPolygonAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Square);
        _publisher.Setup(p => p.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new GameSweepProcessor(
            _games.Object,
            _boundary.Object,
            new RayCastingBoundaryChecker(),
            _publisher.Object,
            _metrics.Object,
            NullLogger<GameSweepProcessor>.Instance);
    }

    private void SetupGame(Game game) =>
        _games.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

    [Fact]
    public async Task ProcessAsync_ShouldReturnNone_WhenGameIsNotInProgress()
    {
        var game = GameFaker.LobbyGameWithPlayers(2, out _); // Lobby state
        SetupGame(game);

        var result = await _sut.ProcessAsync(game.Id, Start, CancellationToken.None);

        Assert.Equal(GameTickResult.None, result);
        _games.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_ShouldBroadcastParticipantsWithKnownLocation_OnRegularTick()
    {
        // Regular tick: now >= NextScheduledBroadcastOn (seeded to Start).
        var game = GameFaker.StartedGame(out _, out var preyIds, Start, playerCount: 3);
        game.RecordLocation(preyIds[0], GpsCoordinate.Create(0, 0), Start.AddSeconds(5));
        SetupGame(game);

        // Sweep at Start+30s → regular tick due (schedule seeded to Start).
        var result = await _sut.ProcessAsync(game.Id, Start.AddSeconds(30), CancellationToken.None);

        // Only the prey that has a location is broadcast; the others have no location yet.
        Assert.Equal(1, result.Broadcasts);
        var prey = game.Participants.Single(p => p.UserId == preyIds[0]);
        Assert.NotNull(prey.Location);
        Assert.All(prey.Locations, l => Assert.True(l.Checked));
        _publisher.Verify(p => p.PublishAsync(
            It.Is<PlayerLocationUpdatedIntegrationEvent>(e => e.UserId == preyIds[0]),
            It.IsAny<CancellationToken>()), Times.Once);
        _games.Verify(r => r.UpdateAsync(game, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_ShouldBroadcastOnEveryRegularTick_EvenWithoutNewReadings()
    {
        // Between ticks without new readings a participant with a known location IS still broadcast
        // on the next regular tick (schedule-driven, not reading-driven).
        var game = GameFaker.StartedGame(out _, out var preyIds, Start, playerCount: 3);
        game.RecordLocation(preyIds[0], GpsCoordinate.Create(0, 0), Start.AddSeconds(5));
        SetupGame(game);

        // First regular tick — broadcasts and consumes the reading.
        await _sut.ProcessAsync(game.Id, Start.AddSeconds(30), CancellationToken.None);
        // Second regular tick (schedule advanced to Start+60s) — no new readings, but still a regular tick.
        var second = await _sut.ProcessAsync(game.Id, Start.AddSeconds(60), CancellationToken.None);

        // Still 1 broadcast because the participant has a known last position.
        Assert.Equal(1, second.Broadcasts);
    }

    [Fact]
    public async Task ProcessAsync_ShouldNotBroadcast_WhenBetweenRegularTicksAndNoActivePenalty()
    {
        // Between regular ticks, a participant with new readings but no penalty must NOT be broadcast.
        var game = GameFaker.StartedGame(out _, out var preyIds, Start, playerCount: 3);
        // First regular tick consumes the initial reading and advances schedule to Start+30s.
        game.RecordLocation(preyIds[0], GpsCoordinate.Create(0, 0), Start.AddSeconds(5));
        SetupGame(game);
        await _sut.ProcessAsync(game.Id, Start.AddSeconds(30), CancellationToken.None);

        // New reading arrives in the gap; sweep at Start+40s — schedule next due at Start+60s.
        game.RecordLocation(preyIds[0], GpsCoordinate.Create(0.1, 0.1), Start.AddSeconds(35));
        var result = await _sut.ProcessAsync(game.Id, Start.AddSeconds(40), CancellationToken.None);

        // Off-beat sweep: new coordinate consumed for boundary check but no broadcast.
        Assert.Equal(0, result.Broadcasts);
    }

    [Fact]
    public async Task ProcessAsync_ShouldPenalizePrey_WhenOutsideBoundaryAndPenaltiesEnabled()
    {
        var config = GameFaker.ValidConfiguration(enablePreyBoundaryPenalties: true);
        var game = GameFaker.StartedGame(out _, out var preyIds, Start, playerCount: 3, configuration: config);
        game.RecordLocation(preyIds[0], GpsCoordinate.Create(5, 5), Start.AddSeconds(5)); // outside the square
        SetupGame(game);

        var now = Start.AddSeconds(30); // regular tick
        var result = await _sut.ProcessAsync(game.Id, now, CancellationToken.None);

        Assert.Equal(1, result.Penalties);
        var prey = game.Participants.Single(p => p.UserId == preyIds[0]);
        Assert.True(prey.HasActivePenalty(now));
        Assert.Equal(now.AddMinutes(Game.PenaltyDurationMinutes), prey.ActivePenaltyEndsAt(now));
        _publisher.Verify(p => p.PublishAsync(
            It.Is<PlayerPenalizedIntegrationEvent>(e => e.UserId == preyIds[0]),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_ShouldPenalize_WhenAnyUncheckedReadingIsOutside()
    {
        var config = GameFaker.ValidConfiguration(enablePreyBoundaryPenalties: true);
        var game = GameFaker.StartedGame(out _, out var preyIds, Start, playerCount: 3, configuration: config);
        game.RecordLocation(preyIds[0], GpsCoordinate.Create(5, 5), Start.AddSeconds(5));  // stepped outside
        game.RecordLocation(preyIds[0], GpsCoordinate.Create(0, 0), Start.AddSeconds(15)); // back inside
        SetupGame(game);

        var result = await _sut.ProcessAsync(game.Id, Start.AddSeconds(30), CancellationToken.None);

        Assert.Equal(1, result.Penalties);
        // The broadcast position is the newest reading (last recorded).
        var prey = game.Participants.Single(p => p.UserId == preyIds[0]);
        Assert.Equal(GpsCoordinate.Create(0, 0), prey.Location);
    }

    [Fact]
    public async Task ProcessAsync_ShouldNotPenalize_WhenPreyIsTagged()
    {
        var config = GameFaker.ValidConfiguration(enablePreyBoundaryPenalties: true);
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start, playerCount: 3, configuration: config);
        game.RecordLocation(preyIds[0], GpsCoordinate.Create(5, 5), Start.AddSeconds(5)); // outside the square
        game.TagParticipant(hunterId, preyIds[0], Start.AddMinutes(10));
        SetupGame(game);

        var result = await _sut.ProcessAsync(game.Id, Start.AddSeconds(30), CancellationToken.None);

        Assert.Equal(0, result.Penalties);
        _publisher.Verify(p => p.PublishAsync(It.IsAny<PlayerPenalizedIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_ShouldBroadcastPenalizedPrey_BetweenRegularTicks()
    {
        // Between regular ticks, a penalized participant must still be broadcast.
        var config = GameFaker.ValidConfiguration(enablePreyBoundaryPenalties: true);
        var game = GameFaker.StartedGame(out _, out var preyIds, Start, playerCount: 3, configuration: config);
        game.RecordLocation(preyIds[0], GpsCoordinate.Create(5, 5), Start.AddSeconds(5)); // outside the square
        SetupGame(game);

        // First regular tick at Start+30s: broadcasts (regular), applies penalty.
        var first = await _sut.ProcessAsync(game.Id, Start.AddSeconds(30), CancellationToken.None);
        // Off-beat tick at Start+45s: schedule next at Start+60s, penalty still active.
        var second = await _sut.ProcessAsync(game.Id, Start.AddSeconds(45), CancellationToken.None);

        Assert.Equal(1, first.Penalties);
        Assert.Equal(1, second.Broadcasts); // re-broadcast while the penalty is active, without new readings
        Assert.Equal(0, second.Penalties);  // no stacking while the penalty is active
        _publisher.Verify(p => p.PublishAsync(
            It.Is<PlayerLocationUpdatedIntegrationEvent>(e => e.UserId == preyIds[0]),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessAsync_ShouldApplyNewPenalty_WhenPreviousExpiredAndStillOutside()
    {
        var config = GameFaker.ValidConfiguration(enablePreyBoundaryPenalties: true);
        var game = GameFaker.StartedGame(out _, out var preyIds, Start, playerCount: 3, configuration: config);
        game.RecordLocation(preyIds[0], GpsCoordinate.Create(5, 5), Start.AddSeconds(5)); // outside the square
        SetupGame(game);

        var first = await _sut.ProcessAsync(game.Id, Start.AddSeconds(30), CancellationToken.None);
        // 6 minutes later the first penalty (5 min) has expired; the last-known position is still outside.
        var afterExpiry = await _sut.ProcessAsync(game.Id, Start.AddMinutes(7), CancellationToken.None);

        Assert.Equal(1, first.Penalties);
        Assert.Equal(1, afterExpiry.Penalties);
        _publisher.Verify(p => p.PublishAsync(It.IsAny<PlayerPenalizedIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessAsync_ShouldNotPenalize_WhenBoundaryPenaltiesDisabled()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start, playerCount: 3); // penalties off by default
        game.RecordLocation(preyIds[0], GpsCoordinate.Create(5, 5), Start.AddSeconds(5));
        SetupGame(game);

        var result = await _sut.ProcessAsync(game.Id, Start.AddSeconds(30), CancellationToken.None);

        Assert.Equal(0, result.Penalties);
    }

    [Fact]
    public async Task ProcessAsync_ShouldNotStackPenalty_WhenPreyAlreadyPenalized()
    {
        var config = GameFaker.ValidConfiguration(enablePreyBoundaryPenalties: true);
        var game = GameFaker.StartedGame(out _, out var preyIds, Start, playerCount: 3, configuration: config);
        game.RecordLocation(preyIds[0], GpsCoordinate.Create(5, 5), Start.AddSeconds(5));
        game.ApplyPenalty(preyIds[0], Start.AddMinutes(5)); // already penalised
        SetupGame(game);

        var result = await _sut.ProcessAsync(game.Id, Start.AddSeconds(30), CancellationToken.None);

        Assert.Equal(0, result.Penalties);
        _publisher.Verify(p => p.PublishAsync(It.IsAny<PlayerPenalizedIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_ShouldCompleteGameAndEmitGameEnded_WhenScheduledEndPassed()
    {
        var game = GameFaker.StartedGame(out _, out _, Start, playerCount: 3); // 60-minute game
        SetupGame(game);

        var result = await _sut.ProcessAsync(game.Id, Start.AddMinutes(61), CancellationToken.None);

        Assert.True(result.Completed);
        Assert.Equal(GameStatus.Completed, game.Status);
        _publisher.Verify(p => p.PublishAsync(It.IsAny<GameEndedIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        _metrics.Verify(m => m.RecordGameCompleted(It.IsAny<string>()), Times.Once);
        _games.Verify(r => r.UpdateAsync(game, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Hunter head-start penalty ──────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_ShouldApplyHeadStartPenalty_WhenHunterMovesBeyond25mDuringDelay()
    {
        // Configuration: 5-minute hunter delay. Default test Start is used.
        var config = GameFaker.ValidConfiguration(hunterDelayTime: 5);
        var game = GameFaker.StartedGame(out var hunterId, out _, Start, configuration: config);

        // Record head-start readings: anchor then a move > 25 m (~111 m north).
        game.RecordLocation(hunterId, GpsCoordinate.Create(52.1, 5.1), Start.AddMinutes(1));
        game.RecordLocation(hunterId, GpsCoordinate.Create(52.101, 5.1), Start.AddMinutes(2));
        SetupGame(game);

        // Sweep during the delay.
        var now = Start.AddMinutes(3);
        var result = await _sut.ProcessAsync(game.Id, now, CancellationToken.None);

        Assert.Equal(1, result.Penalties);
        var hunter = game.Participants.Single(p => p.UserId == hunterId);
        Assert.True(hunter.DelayPenaltyApplied);
        var expectedEndsAt = game.HunterMayMoveAt!.Value.AddMinutes(Game.HunterDelayPenaltyMinutes);
        Assert.True(hunter.HasActivePenalty(now));
        _publisher.Verify(p => p.PublishAsync(
            It.Is<PlayerPenalizedIntegrationEvent>(e =>
                e.GameId == game.Id &&
                e.UserId == hunterId &&
                e.PenaltyEndsAt == expectedEndsAt &&
                e.Reason == "moved-during-delay"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_ShouldApplyHeadStartPenalty_WhenSweepRunsAfterDelayButReadingsAreBefore()
    {
        // Critical correctness: reading is emitted just before HunterMayMoveAt, sweep runs after.
        var config = GameFaker.ValidConfiguration(hunterDelayTime: 5);
        var game = GameFaker.StartedGame(out var hunterId, out _, Start, configuration: config);
        var mayMoveAt = game.HunterMayMoveAt!.Value; // Start + 5 min

        game.RecordLocation(hunterId, GpsCoordinate.Create(52.1, 5.1), Start.AddMinutes(1));
        game.RecordLocation(hunterId, GpsCoordinate.Create(52.101, 5.1), mayMoveAt.AddSeconds(-5)); // still head-start
        SetupGame(game);

        // Sweep arrives 30s after HunterMayMoveAt.
        var now = mayMoveAt.AddSeconds(30);
        var result = await _sut.ProcessAsync(game.Id, now, CancellationToken.None);

        Assert.Equal(1, result.Penalties);
        _publisher.Verify(p => p.PublishAsync(
            It.Is<PlayerPenalizedIntegrationEvent>(e => e.UserId == hunterId && e.Reason == "moved-during-delay"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_ShouldApplyHeadStartPenaltyAtMostOnce_AcrossMultipleSweeps()
    {
        var config = GameFaker.ValidConfiguration(hunterDelayTime: 5);
        var game = GameFaker.StartedGame(out var hunterId, out _, Start, configuration: config);

        game.RecordLocation(hunterId, GpsCoordinate.Create(52.1, 5.1), Start.AddMinutes(1));
        game.RecordLocation(hunterId, GpsCoordinate.Create(52.101, 5.1), Start.AddMinutes(2));
        SetupGame(game);

        // First sweep applies the penalty.
        var first = await _sut.ProcessAsync(game.Id, Start.AddMinutes(3), CancellationToken.None);
        // Second sweep: idempotent — no second penalty.
        var second = await _sut.ProcessAsync(game.Id, Start.AddMinutes(4), CancellationToken.None);

        Assert.Equal(1, first.Penalties);
        Assert.Equal(0, second.Penalties);
        _publisher.Verify(p => p.PublishAsync(
            It.Is<PlayerPenalizedIntegrationEvent>(e => e.Reason == "moved-during-delay"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_ShouldNotApplyHeadStartPenalty_WhenHunterStaysWithin25m()
    {
        var config = GameFaker.ValidConfiguration(hunterDelayTime: 5);
        var game = GameFaker.StartedGame(out var hunterId, out _, Start, configuration: config);

        // ~22 m north: within the 25 m threshold.
        game.RecordLocation(hunterId, GpsCoordinate.Create(52.1, 5.1), Start.AddMinutes(1));
        game.RecordLocation(hunterId, GpsCoordinate.Create(52.1002, 5.1), Start.AddMinutes(2));
        SetupGame(game);

        var result = await _sut.ProcessAsync(game.Id, Start.AddMinutes(3), CancellationToken.None);

        Assert.Equal(0, result.Penalties);
        _publisher.Verify(p => p.PublishAsync(
            It.Is<PlayerPenalizedIntegrationEvent>(e => e.Reason == "moved-during-delay"),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_ShouldNotPersist_WhenNothingChanged()
    {
        // No readings, no penalties, not ended, and the regular tick has no participants with
        // known locations, so no broadcast occurs either.
        var game = GameFaker.StartedGame(out _, out _, Start, playerCount: 3); // no readings, not ended
        SetupGame(game);

        // Sweep slightly before the first regular tick to avoid any broadcast.
        // NextScheduledBroadcastOn = Start; sweep at exactly Start but no known locations.
        // The regular tick is due but no participant has a location, so no broadcasts happen.
        var result = await _sut.ProcessAsync(game.Id, Start.AddSeconds(1), CancellationToken.None);

        _games.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
        _publisher.Verify(p => p.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_ShouldPersist_WhenOnlyOffBeatPenaltyBroadcastOccurs()
    {
        // An off-beat penalty broadcast mutates Location — must persist even without new readings.
        var config = GameFaker.ValidConfiguration(enablePreyBoundaryPenalties: true);
        var game = GameFaker.StartedGame(out _, out var preyIds, Start, playerCount: 3, configuration: config);
        // Give prey a known location and a penalty via a regular tick.
        game.RecordLocation(preyIds[0], GpsCoordinate.Create(5, 5), Start.AddSeconds(5));
        SetupGame(game);

        await _sut.ProcessAsync(game.Id, Start.AddSeconds(30), CancellationToken.None); // first tick: penalty applied
        _games.Invocations.Clear(); // reset mock tracking

        // Off-beat tick: no new readings, penalty still active → must still persist (broadcast happened).
        var result = await _sut.ProcessAsync(game.Id, Start.AddSeconds(45), CancellationToken.None);

        Assert.Equal(1, result.Broadcasts);
        _games.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
