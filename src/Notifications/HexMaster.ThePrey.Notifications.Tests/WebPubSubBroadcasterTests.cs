using Azure;
using Azure.Core;
using Azure.Messaging.WebPubSub;
using HexMaster.ThePrey.IntegrationEvents.Events;
using HexMaster.ThePrey.Notifications;
using HexMaster.ThePrey.Notifications.Observability;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HexMaster.ThePrey.Notifications.Tests;

public sealed class WebPubSubBroadcasterTests
{
    [Fact]
    public async Task SendToGameAsync_ShouldSendJsonToTheGamesGroup()
    {
        var client = new Mock<WebPubSubServiceClient>();
        client
            .Setup(c => c.SendToGroupAsync(
                It.IsAny<string>(), It.IsAny<RequestContent>(), It.IsAny<ContentType>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<RequestContext>()))
            .ReturnsAsync(Mock.Of<Response>());

        var sut = new WebPubSubBroadcaster(client.Object, Mock.Of<INotificationsMetrics>(), NullLogger<WebPubSubBroadcaster>.Instance);
        var gameId = Guid.NewGuid();
        var evt = new PlayerPenalizedIntegrationEvent(gameId, Guid.NewGuid(), DateTimeOffset.UtcNow, "left-playfield");

        await sut.SendToGameAsync(gameId, evt.Topic, evt, CancellationToken.None);

        client.Verify(c => c.SendToGroupAsync(
                gameId.ToString(),
                It.IsAny<RequestContent>(),
                ContentType.ApplicationJson,
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string>(),
                It.IsAny<RequestContext>()),
            Times.Once);
    }

    [Fact]
    public async Task SendToGameAsync_ShouldRecordForwardMetric_OnSuccess()
    {
        var client = new Mock<WebPubSubServiceClient>();
        client
            .Setup(c => c.SendToGroupAsync(
                It.IsAny<string>(), It.IsAny<RequestContent>(), It.IsAny<ContentType>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<RequestContext>()))
            .ReturnsAsync(Mock.Of<Response>());
        var metrics = new Mock<INotificationsMetrics>();

        var sut = new WebPubSubBroadcaster(client.Object, metrics.Object, NullLogger<WebPubSubBroadcaster>.Instance);
        await sut.SendToGameAsync(Guid.NewGuid(), "player-penalized", new { }, CancellationToken.None);

        metrics.Verify(m => m.RecordEventForwarded("player-penalized", It.IsAny<double>()), Times.Once);
        metrics.Verify(m => m.RecordEventForwardFailed(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SendToGameAsync_ShouldRecordFailureMetricAndRethrow_WhenSendFails()
    {
        var client = new Mock<WebPubSubServiceClient>();
        client
            .Setup(c => c.SendToGroupAsync(
                It.IsAny<string>(), It.IsAny<RequestContent>(), It.IsAny<ContentType>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<RequestContext>()))
            .ThrowsAsync(new InvalidOperationException("hub unavailable"));
        var metrics = new Mock<INotificationsMetrics>();

        var sut = new WebPubSubBroadcaster(client.Object, metrics.Object, NullLogger<WebPubSubBroadcaster>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SendToGameAsync(Guid.NewGuid(), "game-ended", new { }, CancellationToken.None));

        metrics.Verify(m => m.RecordEventForwardFailed("game-ended"), Times.Once);
        metrics.Verify(m => m.RecordEventForwarded(It.IsAny<string>(), It.IsAny<double>()), Times.Never);
    }
}
