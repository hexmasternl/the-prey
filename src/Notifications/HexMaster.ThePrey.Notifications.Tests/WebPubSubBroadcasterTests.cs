using Azure;
using Azure.Core;
using Azure.Messaging.WebPubSub;
using HexMaster.ThePrey.IntegrationEvents.Events;
using HexMaster.ThePrey.Notifications;
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

        var sut = new WebPubSubBroadcaster(client.Object, NullLogger<WebPubSubBroadcaster>.Instance);
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
}
