using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Features.RecordPlayerLocation;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Observability;
using HexMaster.ThePrey.Games.Tests.Factories;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.Features;

public sealed class RecordPlayerLocationCommandHandlerTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 3, 12, 0, 0, TimeSpan.Zero);
    // 10 minutes in: before the final stage.
    private static readonly DateTimeOffset Now = Start.AddMinutes(10);

    private readonly Mock<IGameRepository> _repository = new();
    private readonly Mock<IGameMetrics> _metrics = new();
    private readonly Mock<IGameEventBus> _eventBus = new();
    private readonly RecordPlayerLocationCommandHandler _handler;

    public RecordPlayerLocationCommandHandlerTests()
    {
        _eventBus.Setup(b => b.PublishAsync(It.IsAny<Guid>(), It.IsAny<GameEvent>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        _handler = CreateHandler(Now);
    }

    private RecordPlayerLocationCommandHandler CreateHandler(DateTimeOffset now) =>
        new(_repository.Object, _metrics.Object, _eventBus.Object, new FixedTimeProvider(now));

    [Fact]
    public async Task Handle_ShouldRecordAndReturn10sInterval_WhenParticipantSubmits()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start, configuration: GameFaker.ValidConfiguration());
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(
            new RecordPlayerLocationCommand(game.Id, preyIds[0], 52.1, 5.1, null), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.Response.Accepted);
        // Device sampling cadence is now a constant 10s regardless of game stage.
        Assert.Equal(Game.LocationReportingIntervalSeconds, result.Response.NextLocationIntervalSeconds);
        Assert.Null(result.Response.PenaltyIntervalSeconds);
        Assert.Null(result.Response.PenaltyEndsAt);
        _repository.Verify(r => r.UpdateAsync(game, It.IsAny<CancellationToken>()), Times.Once);
        _metrics.Verify(m => m.RecordLocationRecorded(), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnPenaltyInterval_WhenParticipantHasActivePenalty()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start, configuration: GameFaker.ValidConfiguration());
        var penaltyEndsAt = Now.AddMinutes(2);
        game.ApplyPenalty(preyIds[0], penaltyEndsAt);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(
            new RecordPlayerLocationCommand(game.Id, preyIds[0], 52.1, 5.1, null), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.Response.Accepted);
        // Device cadence stays at 10s; penalty info is still surfaced for UI display.
        Assert.Equal(Game.LocationReportingIntervalSeconds, result.Response.NextLocationIntervalSeconds);
        Assert.Equal(Game.PenaltyReportingIntervalSeconds, result.Response.PenaltyIntervalSeconds);
        Assert.Equal(penaltyEndsAt, result.Response.PenaltyEndsAt);
    }

    [Fact]
    public async Task Handle_ShouldOmitPenaltyOverride_WhenPenaltyHasExpired()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start, configuration: GameFaker.ValidConfiguration());
        game.ApplyPenalty(preyIds[0], Now.AddMinutes(-1));
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(
            new RecordPlayerLocationCommand(game.Id, preyIds[0], 52.1, 5.1, null), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result!.Response.PenaltyIntervalSeconds);
        Assert.Null(result.Response.PenaltyEndsAt);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserIsNotAParticipant()
    {
        var game = GameFaker.StartedGame(out _, out _, Start);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(new RecordPlayerLocationCommand(game.Id, Guid.NewGuid(), 52.1, 5.1, null), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenGameNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Game?)null);

        var result = await _handler.Handle(
            new RecordPlayerLocationCommand(Guid.NewGuid(), Guid.NewGuid(), 52.1, 5.1, null), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ShouldNotPublishParticipantLocatedEvent_WhenHunterRecordsLocation()
    {
        // Broadcasts are now sweep-only; ingest must NOT emit ParticipantLocatedEvent.
        var game = GameFaker.StartedGame(out var hunterId, out _, Start, configuration: GameFaker.ValidConfiguration());
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await _handler.Handle(
            new RecordPlayerLocationCommand(game.Id, hunterId, 52.1, 5.1, null), CancellationToken.None);

        _eventBus.Verify(b => b.PublishAsync(
            It.IsAny<Guid>(),
            It.IsAny<ParticipantLocatedEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldNotPublishParticipantLocatedEvent_WhenPreyRecordsLocation()
    {
        // Broadcasts are now sweep-only; ingest must NOT emit ParticipantLocatedEvent.
        var game = GameFaker.StartedGame(out _, out var preyIds, Start, configuration: GameFaker.ValidConfiguration());
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await _handler.Handle(
            new RecordPlayerLocationCommand(game.Id, preyIds[0], 52.2, 5.2, null), CancellationToken.None);

        _eventBus.Verify(b => b.PublishAsync(
            It.IsAny<Guid>(),
            It.IsAny<ParticipantLocatedEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldNotPublishStatusChanged_WhenPreyWasAlreadyActive()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start, configuration: GameFaker.ValidConfiguration());
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await _handler.Handle(
            new RecordPlayerLocationCommand(game.Id, preyIds[0], 52.2, 5.2, null), CancellationToken.None);

        _eventBus.Verify(b => b.PublishAsync(
            It.IsAny<Guid>(),
            It.IsAny<ParticipantStatusChangedEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldPublishStatusChanged_WhenPreyTransitionsFromPassiveToActive()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start, configuration: GameFaker.ValidConfiguration());
        var preyId = preyIds[0];
        var coord = GpsCoordinate.Create(52.1, 5.1);
        // Record location so LastLocationAt is set, then advance time → Passive via timeout
        game.RecordLocation(preyId, coord, Start);
        game.ApplyTimeoutTransitions(Start.AddMinutes(6)); // 6 min silent → Passive (>5 min, <7 min)
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await _handler.Handle(
            new RecordPlayerLocationCommand(game.Id, preyId, 52.2, 5.2, null), CancellationToken.None);

        _eventBus.Verify(b => b.PublishAsync(
            game.Id,
            It.Is<ParticipantStatusChangedEvent>(e =>
                e.ParticipantId == preyId && e.NewState == "Active" && e.ParticipantRole == "Prey"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotChangeState_WhenParticipantIsOut()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start, configuration: GameFaker.ValidConfiguration());
        var preyId = preyIds[0];
        var coord = GpsCoordinate.Create(52.1, 5.1);
        game.RecordLocation(preyId, coord, Start);
        game.ApplyTimeoutTransitions(Start.AddMinutes(8)); // → Out
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await _handler.Handle(
            new RecordPlayerLocationCommand(game.Id, preyId, 52.2, 5.2, null), CancellationToken.None);

        _eventBus.Verify(b => b.PublishAsync(
            It.IsAny<Guid>(),
            It.IsAny<ParticipantStatusChangedEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldNotApplyDelayPenaltyOnDomain_WhenHunterMovesDuringDelay()
    {
        // Head-start penalty is now assessed by the sweep, NOT ingest.
        // RecordLocation no longer applies the penalty — the domain should NOT have a penalty after ingest.
        var game = GameFaker.StartedGame(out var hunterId, out _, Start, configuration: GameFaker.ValidConfiguration());
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        var handler = CreateHandler(Start.AddMinutes(1));

        // First report anchors; second report ~111 m north violates the 25 m threshold.
        await handler.Handle(new RecordPlayerLocationCommand(game.Id, hunterId, 52.1, 5.1, null), CancellationToken.None);
        await handler.Handle(new RecordPlayerLocationCommand(game.Id, hunterId, 52.101, 5.1, null), CancellationToken.None);

        // Ingest must NOT apply the penalty — the sweep's AssessHunterHeadStartPenalty handles it.
        var hunter = game.Participants.Single(p => p.UserId == hunterId);
        Assert.False(hunter.DelayPenaltyApplied);
        Assert.Empty(hunter.Penalties);
        // No status events should have been emitted either.
        _eventBus.Verify(b => b.PublishAsync(
            It.IsAny<Guid>(),
            It.IsAny<ParticipantStatusChangedEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldNotApplyDelayPenaltyOnDomain_WhenHunterStaysWithinThresholdDuringDelay()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, Start, configuration: GameFaker.ValidConfiguration());
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        var handler = CreateHandler(Start.AddMinutes(1));

        // ~22 m north: within the 25 m threshold.
        await handler.Handle(new RecordPlayerLocationCommand(game.Id, hunterId, 52.1, 5.1, null), CancellationToken.None);
        await handler.Handle(new RecordPlayerLocationCommand(game.Id, hunterId, 52.1002, 5.1, null), CancellationToken.None);

        var hunter = game.Participants.Single(p => p.UserId == hunterId);
        Assert.False(hunter.DelayPenaltyApplied);
        Assert.Empty(hunter.Penalties);
    }
}
