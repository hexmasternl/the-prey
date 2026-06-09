using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Features.CompleteGame;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Observability;
using HexMaster.ThePrey.Games.Tests.Factories;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.Features;

public sealed class CompleteGameCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IGameRepository> _repository = new();
    private readonly Mock<IGameEventBus> _eventBus = new();
    private readonly Mock<IGameMetrics> _metrics = new();
    private readonly CompleteGameCommandHandler _handler;

    public CompleteGameCommandHandlerTests()
    {
        _eventBus
            .Setup(b => b.PublishAsync(It.IsAny<Guid>(), It.IsAny<GameEvent>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        _handler = new CompleteGameCommandHandler(
            _repository.Object,
            _eventBus.Object,
            _metrics.Object,
            new FixedTimeProvider(Now));
    }

    [Fact]
    public async Task Handle_ShouldThrowKeyNotFoundException_WhenGameNotFound()
    {
        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Game?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _handler.Handle(new CompleteGameCommand(Guid.NewGuid()), CancellationToken.None));

        _repository.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventBus.Verify(b => b.PublishAsync(It.IsAny<Guid>(), It.IsAny<GameEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnAlreadyCompleted_WhenGameIsAlreadyCompleted()
    {
        var game = GameFaker.StartedGame(out _, out _, Now.AddMinutes(-60));
        game.Complete(Now);

        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new CompleteGameCommand(game.Id), CancellationToken.None);

        Assert.True(result.AlreadyCompleted);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventBus.Verify(b => b.PublishAsync(It.IsAny<Guid>(), It.IsAny<GameEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _metrics.Verify(m => m.RecordGameCompleted(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldCompleteGameAndPersist_WhenGameIsInProgress()
    {
        var game = GameFaker.StartedGame(out _, out _, Now.AddMinutes(-60));

        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new CompleteGameCommand(game.Id), CancellationToken.None);

        Assert.False(result.AlreadyCompleted);
        Assert.Equal(GameStatus.Completed, game.Status);
        Assert.Equal(Now, game.CompletedAt);
        _repository.Verify(r => r.UpdateAsync(game, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldPublishGameEndedEvent_WhenGameIsInProgress()
    {
        var game = GameFaker.StartedGame(out _, out _, Now.AddMinutes(-60));

        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await _handler.Handle(new CompleteGameCommand(game.Id), CancellationToken.None);

        _eventBus.Verify(
            b => b.PublishAsync(game.Id, It.Is<GameEndedEvent>(e => e.GameId == game.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldRecordMetrics_WhenGameCompletes()
    {
        var game = GameFaker.StartedGame(out _, out _, Now.AddMinutes(-60));

        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await _handler.Handle(new CompleteGameCommand(game.Id), CancellationToken.None);

        _metrics.Verify(m => m.RecordGameCompleted(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldSetOutcomePreysWin_WhenNoPreysAreTagged()
    {
        // All preys are alive → preys win
        var game = GameFaker.StartedGame(out _, out _, Now.AddMinutes(-60), playerCount: 3);

        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await _handler.Handle(new CompleteGameCommand(game.Id), CancellationToken.None);

        Assert.Equal(GameOutcome.PreysWin, game.Outcome);
    }

    [Fact]
    public async Task Handle_ShouldSetOutcomeHuntersWin_WhenAllPreysAreTagged()
    {
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Now.AddMinutes(-60), playerCount: 3);
        foreach (var preyId in preyIds)
            game.TagParticipant(hunterId, preyId);

        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await _handler.Handle(new CompleteGameCommand(game.Id), CancellationToken.None);

        Assert.Equal(GameOutcome.HuntersWin, game.Outcome);
    }

    [Fact]
    public async Task Handle_ShouldPublishEventWithCorrectOutcome_WhenGameCompletes()
    {
        var game = GameFaker.StartedGame(out _, out _, Now.AddMinutes(-60), playerCount: 3);

        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await _handler.Handle(new CompleteGameCommand(game.Id), CancellationToken.None);

        _eventBus.Verify(
            b => b.PublishAsync(
                game.Id,
                It.Is<GameEndedEvent>(e => e.Outcome == GameOutcome.PreysWin.ToString()),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCommandIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(null!, CancellationToken.None));
    }
}
