using System.Text;
using System.Text.Json;
using Azure;
using Azure.Core;
using Azure.Messaging.WebPubSub;
using HexMaster.ThePrey.IntegrationEvents;
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

    [Fact]
    public async Task SendToGameAsync_ShouldWrapPayloadInVersionedEnvelope()
    {
        var payloads = CaptureSentEnvelopes(out var client);
        var sut = new WebPubSubBroadcaster(client.Object, Mock.Of<INotificationsMetrics>(), NullLogger<WebPubSubBroadcaster>.Instance);
        var gameId = Guid.NewGuid();

        await sut.SendToGameAsync(gameId, "participant-joined", new { userId = "abc" }, CancellationToken.None);

        var envelope = payloads.Single().RootElement;
        Assert.Equal(RealtimeProtocol.Version, envelope.GetProperty("v").GetInt32());
        Assert.Equal("participant-joined", envelope.GetProperty("type").GetString());
        Assert.Equal(gameId, envelope.GetProperty("gameId").GetGuid());
        Assert.Equal(1, envelope.GetProperty("seq").GetInt64());
        Assert.Equal("abc", envelope.GetProperty("data").GetProperty("userId").GetString());
    }

    [Fact]
    public async Task SendToGameAsync_ShouldAllocateMonotonicSequencePerGame()
    {
        var payloads = CaptureSentEnvelopes(out var client);
        var sut = new WebPubSubBroadcaster(client.Object, Mock.Of<INotificationsMetrics>(), NullLogger<WebPubSubBroadcaster>.Instance);
        var gameA = Guid.NewGuid();
        var gameB = Guid.NewGuid();

        await sut.SendToGameAsync(gameA, "configuration-changed", new { }, CancellationToken.None);
        await sut.SendToGameAsync(gameA, "configuration-changed", new { }, CancellationToken.None);
        await sut.SendToGameAsync(gameB, "configuration-changed", new { }, CancellationToken.None);
        await sut.SendToGameAsync(gameA, "configuration-changed", new { }, CancellationToken.None);

        long SeqAt(int i) => payloads[i].RootElement.GetProperty("seq").GetInt64();
        // Game A gets 1, 2, then 3 (its own counter); game B independently starts at 1.
        Assert.Equal(1, SeqAt(0));
        Assert.Equal(2, SeqAt(1));
        Assert.Equal(1, SeqAt(2));
        Assert.Equal(3, SeqAt(3));
    }

    [Fact]
    public async Task RequestResyncAsync_ShouldBroadcastResyncRequestedWithReason()
    {
        var payloads = CaptureSentEnvelopes(out var client);
        var sut = new WebPubSubBroadcaster(client.Object, Mock.Of<INotificationsMetrics>(), NullLogger<WebPubSubBroadcaster>.Instance);
        var gameId = Guid.NewGuid();

        await sut.RequestResyncAsync(gameId, "sequence-gap", CancellationToken.None);

        var envelope = payloads.Single().RootElement;
        Assert.Equal(RealtimeProtocol.MessageTypes.ResyncRequested, envelope.GetProperty("type").GetString());
        Assert.Equal("sequence-gap", envelope.GetProperty("data").GetProperty("reason").GetString());
    }

    // Captures each envelope the broadcaster sends to the group so tests can assert on the wire shape.
    private static List<JsonDocument> CaptureSentEnvelopes(out Mock<WebPubSubServiceClient> client)
    {
        var captured = new List<JsonDocument>();
        client = new Mock<WebPubSubServiceClient>();
        client
            .Setup(c => c.SendToGroupAsync(
                It.IsAny<string>(), It.IsAny<RequestContent>(), It.IsAny<ContentType>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<RequestContext>()))
            .Callback((string _, RequestContent content, ContentType _, IEnumerable<string> _, string _, RequestContext _) =>
            {
                using var ms = new MemoryStream();
                content.WriteTo(ms, CancellationToken.None);
                captured.Add(JsonDocument.Parse(Encoding.UTF8.GetString(ms.ToArray())));
            })
            .ReturnsAsync(Mock.Of<Response>());
        return captured;
    }
}
