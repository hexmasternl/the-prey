using HexMaster.ThePrey.Games.Abstractions.DataTransferObjects;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.IntegrationEvents;
using HexMaster.ThePrey.IntegrationEvents.Events;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.Features;

public sealed class InProcessLobbyEventBusTests
{
    private readonly Mock<IIntegrationEventPublisher> _integrationPublisherMock = new();
    private readonly InProcessLobbyEventBus _sut;

    public InProcessLobbyEventBusTests()
    {
        _sut = new InProcessLobbyEventBus(_integrationPublisherMock.Object);
    }

    [Fact]
    public async Task PublishAsync_ShouldBridgeToWebPubSub_ViaIntegrationEvent()
    {
        var gameId = Guid.NewGuid();
        var payload = CreatePayload(gameId);

        await _sut.PublishAsync(gameId, "participant-joined", payload);

        _integrationPublisherMock.Verify(p => p.PublishAsync(
            It.Is<LobbyNotificationIntegrationEvent>(e =>
                e.GameId == gameId && e.Name == "participant-joined" && Equals(e.Payload, payload)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_ShouldSwallowException_WhenIntegrationPublisherThrows()
    {
        var gameId = Guid.NewGuid();
        var payload = CreatePayload(gameId);
        _integrationPublisherMock
            .Setup(p => p.PublishAsync(It.IsAny<LobbyNotificationIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker unavailable"));

        var exception = await Record.ExceptionAsync(() => _sut.PublishAsync(gameId, "participant-joined", payload).AsTask());

        Assert.Null(exception);
    }

    private static GameDto CreatePayload(Guid gameId)
        => new(
            gameId,
            "1234",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Lobby",
            new GameConfigurationDto(60, 10, 10, 5, 3, true, true),
            [],
            null,
            [],
            null,
            DateTimeOffset.UtcNow,
            null,
            DateTimeOffset.UtcNow.AddDays(2),
            "Unknown",
            null,
            false,
            false);
}
