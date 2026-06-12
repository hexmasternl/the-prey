using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Features.TagPlayer;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Observability;
using HexMaster.ThePrey.Games.Tests.Factories;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.Features;

public sealed class TagPlayerCommandHandlerTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);

    // Default handler clock: well past the 5-minute hunter head-start delay.
    private static readonly DateTimeOffset Now = Start.AddMinutes(10);

    private readonly Mock<IGameRepository> _repository = new();
    private readonly Mock<IGameEventBus> _eventBus = new();
    private readonly Mock<IGameMetrics> _metrics = new();
    private readonly TagPlayerCommandHandler _handler;

    public TagPlayerCommandHandlerTests()
    {
        _eventBus.Setup(b => b.PublishAsync(It.IsAny<Guid>(), It.IsAny<GameEvent>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        _handler = new TagPlayerCommandHandler(_repository.Object, _eventBus.Object, _metrics.Object, new FixedTimeProvider(Now));
    }

    [Fact]
    public async Task Handle_ShouldTagPreyAndPublishEvent_WhenHunterTagsActiveTarget()
    {
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(
            new TagPlayerCommand(game.Id, hunterId, preyIds[0]), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(PlayerState.Tagged, game.Participants.Single(p => p.UserId == preyIds[0]).State);
        _repository.Verify(r => r.UpdateAsync(game, It.IsAny<CancellationToken>()), Times.Once);
        _eventBus.Verify(b => b.PublishAsync(
            game.Id,
            It.Is<ParticipantStatusChangedEvent>(e =>
                e.ParticipantId == preyIds[0] && e.NewState == "Tagged" && e.ParticipantRole == "Prey"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenGameNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Game?)null);

        var result = await _handler.Handle(
            new TagPlayerCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenCallerIsNotHunter()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _handler.Handle(new TagPlayerCommand(game.Id, preyIds[0], preyIds[1]), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldThrowArgumentException_WhenTargetNotFound()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, Start);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.Handle(new TagPlayerCommand(game.Id, hunterId, Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperation_WhenTargetIsAlreadyTagged()
    {
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start);
        game.TagParticipant(hunterId, preyIds[0], Now);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(new TagPlayerCommand(game.Id, hunterId, preyIds[0]), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperation_WhenTaggingBeforeHunterMayMoveAt()
    {
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        // Clock inside the 5-minute head-start delay.
        var handler = new TagPlayerCommandHandler(
            _repository.Object, _eventBus.Object, _metrics.Object, new FixedTimeProvider(Start.AddMinutes(4)));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new TagPlayerCommand(game.Id, hunterId, preyIds[0]), CancellationToken.None));

        Assert.Equal(PlayerState.Active, game.Participants.Single(p => p.UserId == preyIds[0]).State);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldTagPrey_WhenExactlyAtHunterMayMoveAt()
    {
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        var handler = new TagPlayerCommandHandler(
            _repository.Object, _eventBus.Object, _metrics.Object, new FixedTimeProvider(game.HunterMayMoveAt!.Value));

        var result = await handler.Handle(
            new TagPlayerCommand(game.Id, hunterId, preyIds[0]), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(PlayerState.Tagged, game.Participants.Single(p => p.UserId == preyIds[0]).State);
    }

    [Fact]
    public async Task Handle_ShouldNotEndGame_WhenPreysStillSurvive()
    {
        // 3 players → 1 hunter + 2 preys; tagging one leaves a survivor.
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await _handler.Handle(new TagPlayerCommand(game.Id, hunterId, preyIds[0]), CancellationToken.None);

        Assert.Equal(GameStatus.InProgress, game.Status);
        _eventBus.Verify(b => b.PublishAsync(It.IsAny<Guid>(), It.IsAny<GameEndedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _metrics.Verify(m => m.RecordGameCompleted(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldEndGameAndPublishGameEnded_WhenLastSurvivingPreyIsTagged()
    {
        // 2 players → 1 hunter + 1 prey; tagging that prey leaves no survivors.
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start, playerCount: 2);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(
            new TagPlayerCommand(game.Id, hunterId, preyIds[0]), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(GameStatus.Completed, game.Status);
        Assert.Equal(GameOutcome.HuntersWin, game.Outcome);
        _repository.Verify(r => r.UpdateAsync(game, It.IsAny<CancellationToken>()), Times.Once);
        _eventBus.Verify(b => b.PublishAsync(
            game.Id,
            It.Is<GameEndedEvent>(e => e.Outcome == nameof(GameOutcome.HuntersWin) && e.SurvivorCount == 0),
            It.IsAny<CancellationToken>()), Times.Once);
        _metrics.Verify(m => m.RecordGameCompleted(nameof(GameOutcome.HuntersWin)), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldEndGame_WhenFinalPreyTaggedAfterOthersAreOut()
    {
        // 3 players → 1 hunter + 2 preys. One prey times out (Out); tagging the other ends the game.
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start);
        game.RecordLocation(preyIds[0], GpsCoordinate.Create(52.1, 5.1), Start);
        game.ApplyTimeoutTransitions(Start.AddMinutes(8)); // preyIds[0] → Out
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await _handler.Handle(new TagPlayerCommand(game.Id, hunterId, preyIds[1]), CancellationToken.None);

        Assert.Equal(GameStatus.Completed, game.Status);
        Assert.Equal(GameOutcome.HuntersWin, game.Outcome);
        _eventBus.Verify(b => b.PublishAsync(
            game.Id, It.IsAny<GameEndedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperation_WhenTargetIsOut()
    {
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start);
        var preyId = preyIds[0];
        game.RecordLocation(preyId, GpsCoordinate.Create(52.1, 5.1), Start);
        game.ApplyTimeoutTransitions(Start.AddMinutes(8)); // → Out
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(new TagPlayerCommand(game.Id, hunterId, preyId), CancellationToken.None));
    }
}
