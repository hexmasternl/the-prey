using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Features.EndGame;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Tests.Factories;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.Features;

public sealed class EndGameCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 9, 10, 0, 0, TimeSpan.Zero);

    private readonly Mock<IGameRepository> _repository = new();
    private readonly Mock<IGameEventBus> _eventBus = new();
    private readonly Mock<ILobbyEventBus> _lobbyEventBus = new();
    private readonly EndGameCommandHandler _handler;

    public EndGameCommandHandlerTests()
    {
        _eventBus.Setup(b => b.PublishAsync(It.IsAny<Guid>(), It.IsAny<GameEvent>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        _lobbyEventBus.Setup(b => b.PublishAsync(It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<HexMaster.ThePrey.Games.Abstractions.DataTransferObjects.GameDto>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        _handler = new EndGameCommandHandler(
            _repository.Object,
            _eventBus.Object,
            _lobbyEventBus.Object,
            new FixedTimeProvider(Now));
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenGameNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Game?)null);

        var result = await _handler.Handle(new EndGameCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenRequesterIsNotOwner()
    {
        var game = GameFaker.LobbyGameWithPlayers(2, out _);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _handler.Handle(new EndGameCommand(game.Id, Guid.NewGuid()), CancellationToken.None));

        _repository.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldEndLobbyGame_AndPublishToBothBuses()
    {
        var game = GameFaker.LobbyGameWithPlayers(2, out _);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new EndGameCommand(game.Id, game.OwnerUserId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(GameStatus.Completed, game.Status);
        _repository.Verify(r => r.UpdateAsync(game, It.IsAny<CancellationToken>()), Times.Once);
        _eventBus.Verify(b => b.PublishAsync(game.Id, It.IsAny<GameEndedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        _lobbyEventBus.Verify(b => b.PublishAsync(game.Id, "game-ended",
            It.IsAny<HexMaster.ThePrey.Games.Abstractions.DataTransferObjects.GameDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldEndInProgressGame_PublishGameEventOnly()
    {
        var game = GameFaker.StartedGame(out _, out _, Now);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new EndGameCommand(game.Id, game.OwnerUserId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(GameStatus.Completed, game.Status);
        _repository.Verify(r => r.UpdateAsync(game, It.IsAny<CancellationToken>()), Times.Once);
        _eventBus.Verify(b => b.PublishAsync(game.Id, It.IsAny<GameEndedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        _lobbyEventBus.Verify(b => b.PublishAsync(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<HexMaster.ThePrey.Games.Abstractions.DataTransferObjects.GameDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenGameIsAlreadyCompleted()
    {
        var game = GameFaker.StartedGame(out _, out _, Now);
        game.Complete(Now.AddMinutes(60));
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(new EndGameCommand(game.Id, game.OwnerUserId), CancellationToken.None));

        _repository.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
