using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Features.RemoveLobbyPlayer;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Tests.Factories;
using HexMaster.ThePrey.IntegrationEvents;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.Features;

public sealed class RemoveLobbyPlayerCommandHandlerTests
{
    private readonly Mock<IGameRepository> _repository = new();
    private readonly Mock<ILobbyEventBus> _eventBus = new();
    private readonly RemoveLobbyPlayerCommandHandler _handler;

    public RemoveLobbyPlayerCommandHandlerTests() => _handler = new RemoveLobbyPlayerCommandHandler(_repository.Object, _eventBus.Object);

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenGameNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Game?)null);

        var result = await _handler.Handle(new RemoveLobbyPlayerCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCallerIsNotTheOwner()
    {
        var game = GameFaker.LobbyGameWithPlayers(2, out var ids);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(new RemoveLobbyPlayerCommand(game.Id, Guid.NewGuid(), ids[0]), CancellationToken.None));

        _repository.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldPublishConfigurationChanged_WhenRemovingUnreadyBlockerFlipsLobbyToReady()
    {
        // 3 participants: two ready + hunter, one not ready blocking readiness.
        var game = GameFaker.LobbyGameWithPlayers(3, out var ids, markReady: false);
        game.DesignateHunter(ids[0]);
        game.SetReady(ids[0]);
        game.SetReady(ids[1]);
        // ids[2] intentionally left not ready -> blocks readiness.
        Assert.Equal(GameStatus.Lobby, game.Status);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new RemoveLobbyPlayerCommand(game.Id, game.OwnerUserId, ids[2]), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(GameStatus.Ready, game.Status);
        _eventBus.Verify(b => b.PublishAsync(game.Id, RealtimeProtocol.MessageTypes.ParticipantRemoved,
            It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        _eventBus.Verify(b => b.PublishAsync(game.Id, RealtimeProtocol.MessageTypes.ConfigurationChanged,
            It.IsAny<GameConfigurationChangedDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldPublishConfigurationChanged_WhenRemovingDesignatedHunterFlipsReadyToLobby()
    {
        var game = GameFaker.LobbyGameWithPlayers(2, out var ids);
        game.DesignateHunter(ids[0]);
        Assert.Equal(GameStatus.Ready, game.Status);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new RemoveLobbyPlayerCommand(game.Id, game.OwnerUserId, ids[0]), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(GameStatus.Lobby, game.Status);
        _eventBus.Verify(b => b.PublishAsync(game.Id, RealtimeProtocol.MessageTypes.ParticipantRemoved,
            It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        _eventBus.Verify(b => b.PublishAsync(game.Id, RealtimeProtocol.MessageTypes.ConfigurationChanged,
            It.IsAny<GameConfigurationChangedDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotPublishConfigurationChanged_WhenRemainingRosterStillMeetsPreconditions()
    {
        // 3 ready participants + hunter; removing a non-hunter leaves the hunter and enough ready players.
        var game = GameFaker.LobbyGameWithPlayers(3, out var ids);
        game.DesignateHunter(ids[0]);
        Assert.Equal(GameStatus.Ready, game.Status);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new RemoveLobbyPlayerCommand(game.Id, game.OwnerUserId, ids[2]), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(GameStatus.Ready, game.Status);
        _eventBus.Verify(b => b.PublishAsync(game.Id, RealtimeProtocol.MessageTypes.ConfigurationChanged,
            It.IsAny<GameConfigurationChangedDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
