using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Features.JoinGame;
using HexMaster.ThePrey.Games.Tests.Factories;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.Features;

public sealed class JoinGameCommandHandlerTests
{
    private readonly Mock<IGameRepository> _repository = new();
    private readonly JoinGameCommandHandler _handler;

    public JoinGameCommandHandlerTests() => _handler = new JoinGameCommandHandler(_repository.Object);

    [Fact]
    public async Task Handle_ShouldAddPlayerAndPersist_WhenCodeIsCorrect()
    {
        var game = GameFaker.LobbyGame();
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new JoinGameCommand(game.Id, Guid.NewGuid(), game.GameCode, "Alice", null), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result!.Game.Lobby);
        _repository.Verify(r => r.UpdateAsync(game, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenGameNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Game?)null);

        var result = await _handler.Handle(new JoinGameCommand(Guid.NewGuid(), Guid.NewGuid(), "1234", "Alice", null), CancellationToken.None);

        Assert.Null(result);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenJoinCodeIsWrong()
    {
        var game = GameFaker.LobbyGame(gameCode: "1111");
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(new JoinGameCommand(game.Id, Guid.NewGuid(), "9999", "Bob", null), CancellationToken.None));

        _repository.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenPlayerAlreadyInLobby()
    {
        var game = GameFaker.LobbyGame();
        var existingPlayer = GameFaker.Player();
        game.JoinLobby(existingPlayer);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(new JoinGameCommand(game.Id, existingPlayer.UserId, game.GameCode, existingPlayer.DisplayName, null), CancellationToken.None));

        _repository.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenGameIsNotInLobbyState()
    {
        var started = GameFaker.StartedGame(out _, out _, DateTimeOffset.UtcNow);
        _repository.Setup(r => r.GetByIdAsync(started.Id, It.IsAny<CancellationToken>())).ReturnsAsync(started);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(new JoinGameCommand(started.Id, Guid.NewGuid(), started.GameCode, "Carol", null), CancellationToken.None));

        _repository.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
