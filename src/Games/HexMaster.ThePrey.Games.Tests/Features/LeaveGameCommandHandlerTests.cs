using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Features.LeaveGame;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Tests.Factories;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.Features;

public sealed class LeaveGameCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 9, 10, 0, 0, TimeSpan.Zero);

    private readonly Mock<IGameRepository> _repository = new();
    private readonly Mock<IGameEventBus> _eventBus = new();
    private readonly Mock<ILobbyEventBus> _lobbyEventBus = new();
    private readonly LeaveGameCommandHandler _handler;

    public LeaveGameCommandHandlerTests()
    {
        _eventBus.Setup(b => b.PublishAsync(It.IsAny<Guid>(), It.IsAny<GameEvent>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        _lobbyEventBus.Setup(b => b.PublishAsync(It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<HexMaster.ThePrey.Games.Abstractions.DataTransferObjects.GameDto>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        _handler = new LeaveGameCommandHandler(
            _repository.Object,
            _eventBus.Object,
            _lobbyEventBus.Object,
            new FixedTimeProvider(Now));
    }

    // ---------- general ----------

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenGameNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Game?)null);

        var result = await _handler.Handle(new LeaveGameCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenGameIsCompleted()
    {
        var game = GameFaker.StartedGame(out _, out _, Now);
        game.Complete(Now.AddMinutes(60));
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(new LeaveGameCommand(game.Id, Guid.NewGuid()), CancellationToken.None));
    }

    // ---------- Lobby: non-owner leaves ----------

    [Fact]
    public async Task Handle_ShouldRemovePlayerFromLobby_WhenNonOwnerLeavesLobby()
    {
        var game = GameFaker.LobbyGameWithPlayers(2, out var ids);
        var nonOwner = ids[0]; // ids are lobby players; owner is separate
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new LeaveGameCommand(game.Id, nonOwner), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(GameStatus.Lobby, game.Status);
        Assert.DoesNotContain(game.Participants, p => p.UserId == nonOwner);
        _repository.Verify(r => r.UpdateAsync(game, It.IsAny<CancellationToken>()), Times.Once);
        _lobbyEventBus.Verify(b => b.PublishAsync(game.Id, "lobby-updated",
            It.IsAny<HexMaster.ThePrey.Games.Abstractions.DataTransferObjects.GameDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenNonOwnerNotInLobby()
    {
        var game = GameFaker.LobbyGameWithPlayers(2, out _);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.Handle(new LeaveGameCommand(game.Id, Guid.NewGuid()), CancellationToken.None));

        _repository.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- Lobby: owner leaves (cancels game) ----------

    [Fact]
    public async Task Handle_ShouldEndGame_WhenOwnerLeavesLobby()
    {
        var game = GameFaker.LobbyGameWithPlayers(2, out _);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new LeaveGameCommand(game.Id, game.OwnerUserId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(GameStatus.Completed, game.Status);
        _repository.Verify(r => r.UpdateAsync(game, It.IsAny<CancellationToken>()), Times.Once);
        _eventBus.Verify(b => b.PublishAsync(game.Id, It.IsAny<GameEndedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        _lobbyEventBus.Verify(b => b.PublishAsync(game.Id, "game-ended",
            It.IsAny<HexMaster.ThePrey.Games.Abstractions.DataTransferObjects.GameDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- InProgress: non-participant ----------

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserIsNotAParticipant_InProgress()
    {
        var game = GameFaker.StartedGame(out _, out _, Now);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _handler.Handle(new LeaveGameCommand(game.Id, Guid.NewGuid()), CancellationToken.None));

        _repository.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- InProgress: prey forfeits ----------

    [Fact]
    public async Task Handle_ShouldForfeitPrey_WhenPreyLeavesInProgressGame()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Now);
        var preyId = preyIds[0];
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new LeaveGameCommand(game.Id, preyId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(GameStatus.InProgress, game.Status);
        var prey = game.Participants.Single(p => p.UserId == preyId);
        Assert.Equal(PlayerState.Out, prey.State);
        _repository.Verify(r => r.UpdateAsync(game, It.IsAny<CancellationToken>()), Times.Once);
        _eventBus.Verify(b => b.PublishAsync(game.Id,
            It.Is<ParticipantStatusChangedEvent>(e => e.ParticipantId == preyId && e.NewState == "Out"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- InProgress: hunter leaves (ends game) ----------

    [Fact]
    public async Task Handle_ShouldEndGame_WhenHunterLeavesInProgressGame()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, Now);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new LeaveGameCommand(game.Id, hunterId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(GameStatus.Completed, game.Status);
        _repository.Verify(r => r.UpdateAsync(game, It.IsAny<CancellationToken>()), Times.Once);
        _eventBus.Verify(b => b.PublishAsync(game.Id, It.IsAny<GameEndedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        // Lobby bus must NOT be called for in-progress games.
        _lobbyEventBus.Verify(b => b.PublishAsync(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<HexMaster.ThePrey.Games.Abstractions.DataTransferObjects.GameDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
