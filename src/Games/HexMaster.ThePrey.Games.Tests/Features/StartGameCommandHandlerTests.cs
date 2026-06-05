using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Features.StartGame;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Observability;
using HexMaster.ThePrey.Games.Tests.Factories;
using Microsoft.Extensions.Logging;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.Features;

public sealed class StartGameCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IGameRepository> _repository = new();
    private readonly Mock<IGameMetrics> _metrics = new();
    private readonly Mock<IGameEventBus> _eventBus = new();
    private readonly Mock<ILobbyEventBus> _lobbyEventBus = new();
    private readonly StartGameCommandHandler _handler;

    public StartGameCommandHandlerTests()
    {
        _eventBus.Setup(b => b.PublishAsync(It.IsAny<Guid>(), It.IsAny<GameEvent>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        _lobbyEventBus.Setup(b => b.PublishAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<HexMaster.ThePrey.Games.Abstractions.DataTransferObjects.GameDto>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        _handler = new StartGameCommandHandler(
            _repository.Object,
            _metrics.Object,
            _eventBus.Object,
            _lobbyEventBus.Object,
            new FixedTimeProvider(Now),
            Mock.Of<ILogger<StartGameCommandHandler>>());
    }

    [Fact]
    public async Task Handle_ShouldStartGame_WhenOwnerStartsWithValidHunter()
    {
        var game = GameFaker.LobbyGameWithPlayers(3, out var ids);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new StartGameCommand(game.Id, game.OwnerUserId, ids[0]), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(GameStatus.InProgress.ToString(), result!.Game.Status);
        Assert.Equal(ids[0], result.Game.Hunter!.UserId);
        Assert.Equal(2, result.Game.Preys.Count);
        _repository.Verify(r => r.UpdateAsync(game, It.IsAny<CancellationToken>()), Times.Once);
        _metrics.Verify(m => m.RecordGameStarted(), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenRequesterIsNotOwner()
    {
        var game = GameFaker.LobbyGameWithPlayers(3, out var ids);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(new StartGameCommand(game.Id, Guid.NewGuid(), ids[0]), CancellationToken.None));
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenGameNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Game?)null);

        var result = await _handler.Handle(new StartGameCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }
}
