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
        var payload = new { status = "InProgress" };

        await _sut.PublishAsync(gameId, "configuration-changed", payload);

        _integrationPublisherMock.Verify(p => p.PublishAsync(
            It.Is<GameNotificationIntegrationEvent>(e =>
                e.GameId == gameId && e.Name == "configuration-changed" && Equals(e.Payload, payload)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_ShouldSwallowException_WhenIntegrationPublisherThrows()
    {
        var gameId = Guid.NewGuid();
        var payload = new { outcome = "PreysWin", survivorCount = 1 };
        _integrationPublisherMock
            .Setup(p => p.PublishAsync(It.IsAny<GameNotificationIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker unavailable"));

        var exception = await Record.ExceptionAsync(() => _sut.PublishAsync(gameId, "game-ended", payload).AsTask());

        Assert.Null(exception);
    }
}
