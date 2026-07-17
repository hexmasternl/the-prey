using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.IntegrationEvents;
using HexMaster.ThePrey.IntegrationEvents.Events;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.Features;

public sealed class InProcessGameEventBusTests
{
    private readonly Mock<IIntegrationEventPublisher> _integrationPublisherMock = new();
    private readonly InProcessGameEventBus _sut;

    public InProcessGameEventBusTests()
    {
        _sut = new InProcessGameEventBus(_integrationPublisherMock.Object);
    }

    [Fact]
    public async Task PublishAsync_ShouldBridgeToWebPubSub_ViaIntegrationEvent()
    {
        var gameId = Guid.NewGuid();
        var evt = new StateChangedEvent(gameId, "InProgress");

        await _sut.PublishAsync(gameId, evt);

        _integrationPublisherMock.Verify(p => p.PublishAsync(
            It.Is<GameNotificationIntegrationEvent>(e =>
                e.GameId == gameId && e.Name == evt.EventType && Equals(e.Payload, evt)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_ShouldNotBridgeToWebPubSub_WhenEventIsParticipantLocated()
    {
        // participant-located is high-frequency (one per GPS post); the sweep broadcasts it
        // (throttled) instead, so bridging it here would flood the Web PubSub hub.
        var gameId = Guid.NewGuid();
        var evt = new ParticipantLocatedEvent(gameId, Guid.NewGuid(), "Hunter", 52.0, 5.0, "Active");

        await _sut.PublishAsync(gameId, evt);

        _integrationPublisherMock.Verify(p => p.PublishAsync(
            It.IsAny<GameNotificationIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublishAsync_ShouldSwallowException_WhenIntegrationPublisherThrows()
    {
        var gameId = Guid.NewGuid();
        var evt = new GameEndedEvent(gameId, "PreysWin", 1);
        _integrationPublisherMock
            .Setup(p => p.PublishAsync(It.IsAny<GameNotificationIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker unavailable"));

        var exception = await Record.ExceptionAsync(() => _sut.PublishAsync(gameId, evt).AsTask());

        Assert.Null(exception);
    }
}
