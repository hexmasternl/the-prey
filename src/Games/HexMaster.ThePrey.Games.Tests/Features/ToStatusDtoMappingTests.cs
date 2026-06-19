using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Tests.Factories;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.Features;

/// <summary>Tests for the ToStatusDto mapping path exercised via the GetGameStatusQueryHandler.</summary>
public sealed class ToStatusDtoMappingTests
{
    private static async Task<GameStatusDto?> QueryStatus(Game game, Guid userId)
    {
        var repoMock = new Mock<IGameRepository>();
        var playfieldsMock = new Mock<IPlayfieldInfoProvider>();
        repoMock.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        playfieldsMock.Setup(p => p.GetAsync(game.PlayfieldId, It.IsAny<CancellationToken>())).ReturnsAsync((PlayfieldInfo?)null);
        var handler = new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQueryHandler(repoMock.Object, playfieldsMock.Object);
        return await handler.Handle(new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQuery(game.Id, userId), CancellationToken.None);
    }

    // ── Theme A: CurrentPingInterval ──────────────────────────────────────────

    [Fact]
    public async Task Handle_ShouldReturnCurrentPingInterval_EqualToDefaultLocationInterval_WhenOutsideFinalStage()
    {
        var config = GameFaker.ValidConfiguration(gameDuration: 60, finalStageDuration: 10, defaultLocationInterval: 30);
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5); // well before final stage
        var game = GameFaker.StartedGame(out var hunterId, out _, startedAt, configuration: config);

        var result = await QueryStatus(game, hunterId);

        Assert.NotNull(result);
        Assert.Equal(30, result!.CurrentPingInterval);
    }

    [Fact]
    public async Task Handle_ShouldReturnCurrentPingInterval_EqualToFinalLocationInterval_WhenInFinalStage()
    {
        var config = GameFaker.ValidConfiguration(gameDuration: 60, finalStageDuration: 10, finalLocationInterval: 10);
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-55); // 55 min in → final stage (last 10 min)
        var game = GameFaker.StartedGame(out var hunterId, out _, startedAt, configuration: config);

        var result = await QueryStatus(game, hunterId);

        Assert.NotNull(result);
        Assert.Equal(10, result!.CurrentPingInterval);
    }

    [Fact]
    public async Task Handle_ShouldReturnCurrentPingInterval_EqualToPenaltyInterval_WhenParticipantHasActivePenalty()
    {
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var game = GameFaker.StartedGame(out var hunterId, out _, startedAt);
        // Apply a penalty that is still active.
        game.ApplyPenalty(hunterId, DateTimeOffset.UtcNow.AddMinutes(5));

        var result = await QueryStatus(game, hunterId);

        Assert.NotNull(result);
        Assert.Equal(Game.PenaltyReportingIntervalSeconds, result!.CurrentPingInterval);
    }

    [Fact]
    public async Task Handle_ShouldReturnNextPingDuration_WithinCurrentPingInterval_BeforeFirstPing()
    {
        var config = GameFaker.ValidConfiguration(defaultLocationInterval: 30);
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var game = GameFaker.StartedGame(out var hunterId, out _, startedAt, configuration: config);
        // No location recorded yet, non-penalised → NextPingDuration is driven by the game-wide schedule,
        // so it is somewhere in [0, CurrentPingInterval].

        var result = await QueryStatus(game, hunterId);

        Assert.NotNull(result);
        Assert.InRange(result!.NextPingDuration, 0, result.CurrentPingInterval);
    }

    [Fact]
    public async Task Handle_ShouldReturnNonZeroPingFields_ForParticipant_AndEnsureMappingGuardIsActive()
    {
        // The GetGameStatusQueryHandler blocks non-participants before reaching mapping.
        // Verify that a participant receives non-zero ping fields (confirming the isParticipant
        // branch is taken), while a non-participant call correctly throws.
        var config = GameFaker.ValidConfiguration(defaultLocationInterval: 30);
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var game = GameFaker.StartedGame(out var hunterId, out _, startedAt, configuration: config);

        // Participant: should get non-zero CurrentPingInterval
        var participantResult = await QueryStatus(game, hunterId);

        Assert.NotNull(participantResult);
        Assert.True(participantResult!.CurrentPingInterval > 0, "Participant should have non-zero CurrentPingInterval.");
        Assert.True(participantResult.NextPingDuration >= 0, "NextPingDuration must be >= 0.");
        Assert.True(participantResult.NextPingDuration <= participantResult.CurrentPingInterval,
            "NextPingDuration must not exceed CurrentPingInterval.");

        // Non-participant: should be rejected before reaching mapping (access guard test).
        var nonParticipant = Guid.NewGuid();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => QueryStatus(game, nonParticipant));
    }


    [Fact]
    public async Task Handle_ShouldReturnIsEndgame_True_WhenInFinalStage()
    {
        // A game with 60 min duration, 10 min final stage: start 55 min ago puts us in final stage.
        var config = GameFaker.ValidConfiguration(gameDuration: 60, finalStageDuration: 10);
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-55);
        var game = GameFaker.StartedGame(out var hunterId, out _, startedAt, configuration: config);

        var repoMock = new Mock<IGameRepository>();
        var playfieldsMock = new Mock<IPlayfieldInfoProvider>();

        repoMock.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        playfieldsMock.Setup(p => p.GetAsync(game.PlayfieldId, It.IsAny<CancellationToken>())).ReturnsAsync((PlayfieldInfo?)null);

        var handler = new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQueryHandler(repoMock.Object, playfieldsMock.Object);
        var result = await handler.Handle(new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsEndgame);
    }

    [Fact]
    public async Task Handle_ShouldReturnIsEndgame_False_WhenNotInFinalStage()
    {
        var config = GameFaker.ValidConfiguration(gameDuration: 60, finalStageDuration: 10);
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5); // 5 minutes in, not in final stage
        var game = GameFaker.StartedGame(out var hunterId, out _, startedAt, configuration: config);

        var repoMock = new Mock<IGameRepository>();
        var playfieldsMock = new Mock<IPlayfieldInfoProvider>();

        repoMock.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        playfieldsMock.Setup(p => p.GetAsync(game.PlayfieldId, It.IsAny<CancellationToken>())).ReturnsAsync((PlayfieldInfo?)null);

        var handler = new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQueryHandler(repoMock.Object, playfieldsMock.Object);
        var result = await handler.Handle(new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result!.IsEndgame);
    }

    [Fact]
    public async Task Handle_ShouldReturnHunterMayMoveAt_AsStartPlusHunterDelayTime()
    {
        var config = GameFaker.ValidConfiguration(hunterDelayTime: 5);
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        var game = GameFaker.StartedGame(out var hunterId, out _, startedAt, configuration: config);

        var repoMock = new Mock<IGameRepository>();
        var playfieldsMock = new Mock<IPlayfieldInfoProvider>();

        repoMock.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        playfieldsMock.Setup(p => p.GetAsync(game.PlayfieldId, It.IsAny<CancellationToken>())).ReturnsAsync((PlayfieldInfo?)null);

        var handler = new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQueryHandler(repoMock.Object, playfieldsMock.Object);
        var result = await handler.Handle(new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(startedAt.AddMinutes(5), result!.HunterMayMoveAt);
    }

    [Fact]
    public async Task Handle_ShouldReturnParticipantCallsign_FromLobbyDisplayName()
    {
        var game = GameFaker.LobbyGame();
        var playerId = Guid.NewGuid();
        var expectedCallsign = "TestCallsign";
        game.JoinLobby(GameFaker.Player(playerId, expectedCallsign));
        var secondPlayer = Guid.NewGuid();
        game.JoinLobby(GameFaker.Player(secondPlayer));
        game.SetReady(playerId);
        game.SetReady(secondPlayer);
        game.Arm(playerId);
        game.BeginPlay(DateTimeOffset.UtcNow.AddMinutes(-5));

        var repoMock = new Mock<IGameRepository>();
        var playfieldsMock = new Mock<IPlayfieldInfoProvider>();

        repoMock.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        playfieldsMock.Setup(p => p.GetAsync(game.PlayfieldId, It.IsAny<CancellationToken>())).ReturnsAsync((PlayfieldInfo?)null);

        var handler = new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQueryHandler(repoMock.Object, playfieldsMock.Object);
        var result = await handler.Handle(new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQuery(game.Id, playerId), CancellationToken.None);

        Assert.NotNull(result);
        var hunterStatus = result!.Participants.Single(p => p.UserId == playerId);
        Assert.Equal(expectedCallsign, hunterStatus.Callsign);
    }

    [Fact]
    public async Task Handle_ShouldReturnNullLastKnownLocation_WhenParticipantHasNoLocation()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, DateTimeOffset.UtcNow.AddMinutes(-5));

        var repoMock = new Mock<IGameRepository>();
        var playfieldsMock = new Mock<IPlayfieldInfoProvider>();

        repoMock.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        playfieldsMock.Setup(p => p.GetAsync(game.PlayfieldId, It.IsAny<CancellationToken>())).ReturnsAsync((PlayfieldInfo?)null);

        var handler = new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQueryHandler(repoMock.Object, playfieldsMock.Object);
        var result = await handler.Handle(new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        var hunterStatus = result!.Participants.Single(p => p.UserId == hunterId);
        Assert.Null(hunterStatus.LastKnownLocation);
    }

    [Fact]
    public async Task Handle_ShouldSetHunterStateToActive_Always()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, DateTimeOffset.UtcNow.AddMinutes(-5));

        var repoMock = new Mock<IGameRepository>();
        var playfieldsMock = new Mock<IPlayfieldInfoProvider>();
        repoMock.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        playfieldsMock.Setup(p => p.GetAsync(game.PlayfieldId, It.IsAny<CancellationToken>())).ReturnsAsync((PlayfieldInfo?)null);

        var handler = new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQueryHandler(repoMock.Object, playfieldsMock.Object);
        var result = await handler.Handle(new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        var hunterStatus = result!.Participants.Single(p => p.UserId == hunterId);
        Assert.Equal("Active", hunterStatus.State);
    }

    [Fact]
    public async Task Handle_ShouldSetPreyStateToTagged_WhenPreyIsTagged()
    {
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, startedAt);
        var preyId = preyIds[0];
        GameFaker.RecordColocated(game, startedAt.AddMinutes(1), hunterId, preyId);
        game.TagParticipant(hunterId, preyId, startedAt.AddMinutes(10));

        var repoMock = new Mock<IGameRepository>();
        var playfieldsMock = new Mock<IPlayfieldInfoProvider>();
        repoMock.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        playfieldsMock.Setup(p => p.GetAsync(game.PlayfieldId, It.IsAny<CancellationToken>())).ReturnsAsync((PlayfieldInfo?)null);

        var handler = new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQueryHandler(repoMock.Object, playfieldsMock.Object);
        var result = await handler.Handle(new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        var taggedPrey = result!.Participants.Single(p => p.UserId == preyId);
        Assert.Equal("Tagged", taggedPrey.State);
    }

    [Fact]
    public async Task Handle_ShouldCountOnlyActiveAndPassivePreys_InPreysLeft()
    {
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        // 3 preys total
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, startedAt, playerCount: 4);

        // Tag one prey
        GameFaker.RecordColocated(game, startedAt.AddMinutes(1), hunterId, preyIds[0]);
        game.TagParticipant(hunterId, preyIds[0], startedAt.AddMinutes(10));
        // Set one to Out via timeout
        game.RecordLocation(preyIds[1], GpsCoordinate.Create(52.0, 5.0), startedAt);
        game.ApplyTimeoutTransitions(startedAt.AddMinutes(8)); // → Out

        var repoMock = new Mock<IGameRepository>();
        var playfieldsMock = new Mock<IPlayfieldInfoProvider>();
        repoMock.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        playfieldsMock.Setup(p => p.GetAsync(game.PlayfieldId, It.IsAny<CancellationToken>())).ReturnsAsync((PlayfieldInfo?)null);

        var handler = new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQueryHandler(repoMock.Object, playfieldsMock.Object);
        var result = await handler.Handle(new HexMaster.ThePrey.Games.Features.GetGameStatus.GetGameStatusQuery(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        // 3 preys, 1 Tagged, 1 Out → PreysLeft = 1 Active
        Assert.Equal(1, result!.PreysLeft);
    }

    // ── Theme B: NextPingDuration hybrid rule ────────────────────────────────

    [Fact]
    public async Task Handle_ShouldReturnNextPingDuration_EqualToSecondsUntilScheduledBroadcast_WhenNonPenalisedAndBroadcastInFuture()
    {
        // Arrange: game just started at "now - 1 second" so NextScheduledBroadcastOn == startedAt.
        // With interval=30 the normalisation loop will advance it to startedAt+30 (29 seconds away).
        var interval = 30;
        var config = GameFaker.ValidConfiguration(defaultLocationInterval: interval);
        var now = DateTimeOffset.UtcNow;
        var startedAt = now.AddSeconds(-1);
        var game = GameFaker.StartedGame(out var hunterId, out _, startedAt, configuration: config);
        // NextScheduledBroadcastOn == startedAt (1 second in the past).
        // After normalisation: scheduled = startedAt + 30s = now + 29s.
        // Expected NextPingDuration == 29.

        var result = await QueryStatus(game, hunterId);

        Assert.NotNull(result);
        // Allow ±1 s for test execution time.
        Assert.InRange(result!.NextPingDuration, 28, 29);
    }

    [Fact]
    public async Task Handle_ShouldReturnNextPingDuration_WithinInterval_WhenNonPenalisedAndScheduledBroadcastIsStale()
    {
        // Arrange: game started long ago → NextScheduledBroadcastOn is well in the past.
        // The normalisation loop must produce a value in [0, interval], never negative.
        var interval = 30;
        var config = GameFaker.ValidConfiguration(defaultLocationInterval: interval);
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var game = GameFaker.StartedGame(out var hunterId, out _, startedAt, configuration: config);

        var result = await QueryStatus(game, hunterId);

        Assert.NotNull(result);
        Assert.InRange(result!.NextPingDuration, 0, interval);
    }

    [Fact]
    public async Task Handle_ShouldReturnNextPingDuration_EqualToSecondsUntilPersonalDeadline_WhenPenalisedAndHasLocation()
    {
        // Arrange: penalty interval is 10 s. Record a location 4 seconds ago → 6 seconds left.
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var game = GameFaker.StartedGame(out var hunterId, out _, startedAt);
        var locationTime = DateTimeOffset.UtcNow.AddSeconds(-4);
        game.RecordLocation(hunterId, GpsCoordinate.Create(52.1, 5.1), locationTime);
        game.ApplyPenalty(hunterId, DateTimeOffset.UtcNow.AddMinutes(5));

        var result = await QueryStatus(game, hunterId);

        Assert.NotNull(result);
        // secondsLeft = locationTime + 10 - now ≈ 6; allow ±1 s for execution time.
        Assert.InRange(result!.NextPingDuration, 5, 6);
        Assert.Equal(Game.PenaltyReportingIntervalSeconds, result.CurrentPingInterval);
    }

    [Fact]
    public async Task Handle_ShouldReturnNextPingDuration_EqualToPenaltyInterval_WhenPenalisedAndNoLocationRecorded()
    {
        // Arrange: active penalty, but participant has never emitted a location.
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var game = GameFaker.StartedGame(out var hunterId, out _, startedAt);
        game.ApplyPenalty(hunterId, DateTimeOffset.UtcNow.AddMinutes(5));
        // No RecordLocation call.

        var result = await QueryStatus(game, hunterId);

        Assert.NotNull(result);
        Assert.Equal(Game.PenaltyReportingIntervalSeconds, result!.NextPingDuration);
    }
}
