using HexMaster.ThePrey.Core;
using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Features.UpdateLocationBroadcast;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Tests.Factories;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.Features;

public sealed class GameEngineEndpointTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IGameRepository> _repository = new();
    private readonly Mock<IGameEngineEventBus> _engineEventBus = new();
    private readonly UpdateLocationBroadcastCommandHandler _handler;

    public GameEngineEndpointTests()
    {
        _engineEventBus.Setup(b => b.PublishAsync(It.IsAny<Guid>(), It.IsAny<EngineLocationEvent>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        _handler = new UpdateLocationBroadcastCommandHandler(_repository.Object, _engineEventBus.Object);
    }

    [Fact]
    public async Task Handle_ShouldThrowKeyNotFoundException_WhenGameNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Game?)null);

        var command = new UpdateLocationBroadcastCommand(
            Guid.NewGuid(),
            [new ParticipantLocationUpdate(Guid.NewGuid(), 52.0, 4.0)]);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperationException_WhenGameIsNotInProgress()
    {
        var game = GameFaker.LobbyGame();
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var command = new UpdateLocationBroadcastCommand(
            game.Id,
            [new ParticipantLocationUpdate(Guid.NewGuid(), 52.0, 4.0)]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldPublishEvents_ForValidParticipants()
    {
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Now);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var locations = new List<ParticipantLocationUpdate>
        {
            new(hunterId, 52.0, 4.0),
            new(preyIds[0], 52.1, 4.1)
        };

        var command = new UpdateLocationBroadcastCommand(game.Id, locations);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);
        _engineEventBus.Verify(
            b => b.PublishAsync(game.Id, It.IsAny<EngineLocationEvent>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_ShouldSilentlyIgnore_NonParticipantUserIds()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, Now);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var locations = new List<ParticipantLocationUpdate>
        {
            new(hunterId, 52.0, 4.0),
            new(Guid.NewGuid(), 99.0, 99.0) // non-participant
        };

        var command = new UpdateLocationBroadcastCommand(game.Id, locations);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);
        // Only 1 event published — the non-participant is silently skipped
        _engineEventBus.Verify(
            b => b.PublishAsync(game.Id, It.IsAny<EngineLocationEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenAllLocationsAreForNonParticipants()
    {
        var game = GameFaker.StartedGame(out _, out _, Now);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var locations = new List<ParticipantLocationUpdate>
        {
            new(Guid.NewGuid(), 52.0, 4.0),
            new(Guid.NewGuid(), 52.1, 4.1)
        };

        var command = new UpdateLocationBroadcastCommand(game.Id, locations);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);
        _engineEventBus.Verify(
            b => b.PublishAsync(It.IsAny<Guid>(), It.IsAny<EngineLocationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
