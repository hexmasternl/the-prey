using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
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
    private readonly Mock<IGameRepository> _repository = new();
    private readonly Mock<IGameMetrics> _metrics = new();
    private readonly Mock<ILobbyEventBus> _lobbyEventBus = new();
    private readonly StartGameCommandHandler _handler;

    public StartGameCommandHandlerTests()
    {
        _lobbyEventBus.Setup(b => b.PublishAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        _handler = new StartGameCommandHandler(
            _repository.Object,
            _metrics.Object,
            _lobbyEventBus.Object,
            Mock.Of<ILogger<StartGameCommandHandler>>());
    }

    [Fact]
    public async Task Handle_ShouldArmGame_WhenOwnerStartsWithValidHunter()
    {
        var game = GameFaker.LobbyGameWithPlayers(3, out var ids);
        game.DesignateHunter(ids[0]); // game reaches Ready — the state from which the owner may start
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new StartGameCommand(game.Id, game.OwnerUserId, ids[0]), CancellationToken.None);

        Assert.NotNull(result);
        // Handler arms the game → Started; the sweep will promote to InProgress.
        Assert.Equal(GameStatus.Started.ToString(), result!.Game.Status);
        Assert.Equal(ids[0], result.Game.HunterUserId);
        Assert.Equal(2, result.Game.Preys.Count);
        Assert.Null(result.Game.StartedAt);
        Assert.Null(result.Game.EndsAt);
        _repository.Verify(r => r.UpdateAsync(game, It.IsAny<CancellationToken>()), Times.Once);
        _metrics.Verify(m => m.RecordGameStarted(), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldPublishConfigurationChanged_WhenArmed()
    {
        var game = GameFaker.LobbyGameWithPlayers(3, out var ids);
        game.DesignateHunter(ids[0]);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await _handler.Handle(new StartGameCommand(game.Id, game.OwnerUserId, ids[0]), CancellationToken.None);

        _lobbyEventBus.Verify(b => b.PublishAsync(
            game.Id,
            "configuration-changed",
            It.Is<GameConfigurationChangedDto>(d => d.Status == GameStatus.Started.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
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
    public async Task Handle_ShouldThrowAndNotStart_WhenAPlayerIsNotReady()
    {
        var game = GameFaker.LobbyGameWithPlayers(3, out var ids, markReady: false);
        game.SetReady(ids[0]);
        game.SetReady(ids[1]); // ids[2] never readies up
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(new StartGameCommand(game.Id, game.OwnerUserId, ids[0]), CancellationToken.None));

        Assert.Equal(GameStatus.Lobby, game.Status);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
        _metrics.Verify(m => m.RecordGameStarted(), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenGameNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Game?)null);

        var result = await _handler.Handle(new StartGameCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ShouldRecordStarted_AfterGameIsPersisted()
    {
        var game = GameFaker.LobbyGameWithPlayers(3, out var ids);
        game.DesignateHunter(ids[0]);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var callOrder = new List<string>();
        _repository.Setup(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("persist"))
            .Returns(Task.CompletedTask);
        _metrics.Setup(m => m.RecordGameStarted())
            .Callback(() => callOrder.Add("metric"));

        await _handler.Handle(new StartGameCommand(game.Id, game.OwnerUserId, ids[0]), CancellationToken.None);

        Assert.Equal(["persist", "metric"], callOrder);
    }
}
