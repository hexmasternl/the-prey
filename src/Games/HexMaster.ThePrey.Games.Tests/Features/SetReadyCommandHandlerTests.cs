using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Features.SetReady;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Tests.Factories;
using HexMaster.ThePrey.IntegrationEvents;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.Features;

public sealed class SetReadyCommandHandlerTests
{
    private readonly Mock<IGameRepository> _repository = new();
    private readonly Mock<ILobbyEventBus> _eventBus = new();
    private readonly SetReadyCommandHandler _handler;

    public SetReadyCommandHandlerTests() => _handler = new SetReadyCommandHandler(_repository.Object, _eventBus.Object);

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenGameNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Game?)null);

        var result = await _handler.Handle(new SetReadyCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ShouldPublishConfigurationChanged_WhenLastPlayerReadyingUpFlipsLobbyToReady()
    {
        // 2 players, neither ready, no hunter designated; designate a hunter so only readiness is missing.
        var game = GameFaker.LobbyGameWithPlayers(2, out var ids, markReady: false);
        game.DesignateHunter(ids[0]);
        game.SetReady(ids[0]); // ids[0] ready; ids[1] still not ready -> game remains Lobby
        Assert.Equal(GameStatus.Lobby, game.Status);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new SetReadyCommand(game.Id, ids[1]), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(GameStatus.Ready, game.Status);
        _eventBus.Verify(b => b.PublishAsync(game.Id, RealtimeProtocol.MessageTypes.ParticipantChanged,
            It.IsAny<ParticipantDto>(), It.IsAny<CancellationToken>()), Times.Once);
        _eventBus.Verify(b => b.PublishAsync(game.Id, RealtimeProtocol.MessageTypes.ConfigurationChanged,
            It.IsAny<GameConfigurationChangedDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotPublishConfigurationChanged_WhenOtherParticipantsStillNotReady()
    {
        // 3 players, none ready, hunter designated; readying one of them still leaves the third unready.
        var game = GameFaker.LobbyGameWithPlayers(3, out var ids, markReady: false);
        game.DesignateHunter(ids[0]);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new SetReadyCommand(game.Id, ids[1]), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(GameStatus.Lobby, game.Status);
        _eventBus.Verify(b => b.PublishAsync(game.Id, RealtimeProtocol.MessageTypes.ParticipantChanged,
            It.IsAny<ParticipantDto>(), It.IsAny<CancellationToken>()), Times.Once);
        _eventBus.Verify(b => b.PublishAsync(game.Id, RealtimeProtocol.MessageTypes.ConfigurationChanged,
            It.IsAny<GameConfigurationChangedDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldNotPublishConfigurationChanged_WhenGameIsAlreadyReady()
    {
        // Re-readying a participant who is already ready is a no-op for readiness.
        var game = GameFaker.LobbyGameWithPlayers(2, out var ids);
        game.DesignateHunter(ids[0]);
        Assert.Equal(GameStatus.Ready, game.Status);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _handler.Handle(new SetReadyCommand(game.Id, ids[0]), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(GameStatus.Ready, game.Status);
        _eventBus.Verify(b => b.PublishAsync(game.Id, RealtimeProtocol.MessageTypes.ConfigurationChanged,
            It.IsAny<GameConfigurationChangedDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
