using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Features.SetHunter;
using HexMaster.ThePrey.Games.Tests.Factories;
using Microsoft.Extensions.Logging;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.Features;

public sealed class SetHunterCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IGameRepository> _repository = new();
    private readonly SetHunterCommandHandler _handler;

    public SetHunterCommandHandlerTests()
    {
        _handler = new SetHunterCommandHandler(
            _repository.Object,
            Mock.Of<ILogger<SetHunterCommandHandler>>());
    }

    [Fact]
    public async Task Handle_ShouldSwapRoles_WhenCallerIsHunterAndTargetIsPrey()
    {
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Now);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new SetHunterCommand(game.Id, hunterId, preyIds[0]), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(preyIds[0], result!.Game.Hunter!.UserId);
        Assert.Contains(result.Game.Preys, p => p.UserId == hunterId);
        Assert.Equal(GameStatus.InProgress.ToString(), result.Game.Status);
        _repository.Verify(r => r.UpdateAsync(game, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenGameNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Game?)null);

        var result = await _handler.Handle(new SetHunterCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenCallerIsNotTheHunter()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Now);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new SetHunterCommand(game.Id, preyIds[0], preyIds[1]), CancellationToken.None);

        Assert.Null(result);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenCallerIsNotAParticipant()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Now);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new SetHunterCommand(game.Id, Guid.NewGuid(), preyIds[0]), CancellationToken.None);

        Assert.Null(result);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenGameIsInLobby()
    {
        var game = GameFaker.LobbyGameWithPlayers(3, out var ids);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new SetHunterCommand(game.Id, ids[0], ids[1]), CancellationToken.None);

        Assert.Null(result);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenGameIsCompleted()
    {
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Now);
        game.Complete(Now.AddHours(1));
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new SetHunterCommand(game.Id, hunterId, preyIds[0]), CancellationToken.None);

        Assert.Null(result);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTargetIsNotAPrey()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, Now);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.Handle(new SetHunterCommand(game.Id, hunterId, Guid.NewGuid()), CancellationToken.None));
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
