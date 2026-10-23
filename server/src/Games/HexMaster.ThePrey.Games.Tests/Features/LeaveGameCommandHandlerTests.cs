using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Features.LeaveGame;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Tests.Factories;
using HexMaster.ThePrey.IntegrationEvents;
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
        _eventBus.Setup(b => b.PublishAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        _lobbyEventBus.Setup(b => b.PublishAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
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
        _lobbyEventBus.Verify(b => b.PublishAsync(game.Id, RealtimeProtocol.MessageTypes.ParticipantRemoved,
            It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
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

    [Fact]
    public async Task Handle_ShouldPublishConfigurationChanged_WhenRemovingDesignatedHunterFlipsReadyToLobby()
    {
        var game = GameFaker.LobbyGameWithPlayers(2, out var ids);
        game.DesignateHunter(ids[0]);
        Assert.Equal(GameStatus.Ready, game.Status);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new LeaveGameCommand(game.Id, ids[0]), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(GameStatus.Lobby, game.Status);
        _lobbyEventBus.Verify(b => b.PublishAsync(game.Id, RealtimeProtocol.MessageTypes.ParticipantRemoved,
            It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        _lobbyEventBus.Verify(b => b.PublishAsync(game.Id, RealtimeProtocol.MessageTypes.ConfigurationChanged,
            It.IsAny<GameConfigurationChangedDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldPublishConfigurationChanged_WhenRemovingUnreadyBlockerFlipsLobbyToReady()
    {
        var game = GameFaker.LobbyGameWithPlayers(3, out var ids, markReady: false);
        game.DesignateHunter(ids[0]);
        game.SetReady(ids[0]);
        game.SetReady(ids[1]);
        // ids[2] intentionally left not ready -> blocks readiness.
        Assert.Equal(GameStatus.Lobby, game.Status);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new LeaveGameCommand(game.Id, ids[2]), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(GameStatus.Ready, game.Status);
        _lobbyEventBus.Verify(b => b.PublishAsync(game.Id, RealtimeProtocol.MessageTypes.ParticipantRemoved,
            It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        _lobbyEventBus.Verify(b => b.PublishAsync(game.Id, RealtimeProtocol.MessageTypes.ConfigurationChanged,
            It.IsAny<GameConfigurationChangedDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotPublishConfigurationChanged_WhenRemainingRosterStillMeetsPreconditions()
    {
        var game = GameFaker.LobbyGameWithPlayers(3, out var ids);
        game.DesignateHunter(ids[0]);
        Assert.Equal(GameStatus.Ready, game.Status);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new LeaveGameCommand(game.Id, ids[2]), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(GameStatus.Ready, game.Status);
        _lobbyEventBus.Verify(b => b.PublishAsync(game.Id, RealtimeProtocol.MessageTypes.ConfigurationChanged,
            It.IsAny<GameConfigurationChangedDto>(), It.IsAny<CancellationToken>()), Times.Never);
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
        _eventBus.Verify(b => b.PublishAsync(game.Id, RealtimeProtocol.MessageTypes.GameEnded,
            It.IsAny<GameEndedNotificationDto>(), It.IsAny<CancellationToken>()), Times.Once);
        _lobbyEventBus.Verify(b => b.PublishAsync(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- Once under way, leaving is rejected outright ----------
    // A game that has started ends only on its own terms (last prey caught, or duration elapsed), so
    // no participant — prey, hunter, or owner — can leave it or take it down by leaving.

    [Fact]
    public async Task Handle_ShouldRejectPrey_WhenGameIsInProgress()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Now);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(new LeaveGameCommand(game.Id, preyIds[0]), CancellationToken.None));

        // Nothing persisted, nothing broadcast, and the prey is still in play.
        Assert.Equal(GameStatus.InProgress, game.Status);
        var prey = game.Participants.Single(p => p.UserId == preyIds[0]);
        Assert.True(prey.State is PlayerState.Active or PlayerState.Passive, "the prey must still be in play");
        VerifyNothingHappened();
    }

    [Fact]
    public async Task Handle_ShouldRejectHunter_WhenGameIsInProgress()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, Now);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(new LeaveGameCommand(game.Id, hunterId), CancellationToken.None));

        Assert.Equal(GameStatus.InProgress, game.Status);
        VerifyNothingHappened();
    }

    [Fact]
    public async Task Handle_ShouldRejectOwner_WhenGameIsInProgress()
    {
        var game = GameFaker.StartedGame(out _, out _, Now);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(new LeaveGameCommand(game.Id, game.OwnerUserId), CancellationToken.None));

        Assert.Equal(GameStatus.InProgress, game.Status);
        VerifyNothingHappened();
    }

    [Fact]
    public async Task Handle_ShouldRejectNonParticipant_WhenGameIsInProgress()
    {
        var game = GameFaker.StartedGame(out _, out _, Now);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(new LeaveGameCommand(game.Id, Guid.NewGuid()), CancellationToken.None));

        VerifyNothingHappened();
    }

    // Started is the transient armed state between the owner pressing start and the sweep promoting the
    // game to InProgress. Leaving must be refused there too, otherwise the answer would depend on which
    // side of a sweep tick the request landed on.
    [Fact]
    public async Task Handle_ShouldReject_WhenGameIsStartedButNotYetPromoted()
    {
        var game = GameFaker.ArmedGame(out var hunterId, out var preyIds);
        Assert.Equal(GameStatus.Started, game.Status);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(new LeaveGameCommand(game.Id, preyIds[0]), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(new LeaveGameCommand(game.Id, hunterId), CancellationToken.None));

        Assert.Equal(GameStatus.Started, game.Status);
        VerifyNothingHappened();
    }

    /// <summary>Asserts a rejected leave was a complete no-op: no write, no broadcast on either bus.</summary>
    private void VerifyNothingHappened()
    {
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventBus.Verify(b => b.PublishAsync(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        _lobbyEventBus.Verify(b => b.PublishAsync(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
