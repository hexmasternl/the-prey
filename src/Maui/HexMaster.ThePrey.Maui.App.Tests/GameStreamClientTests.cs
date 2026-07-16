using System.Diagnostics;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Realtime;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class GameStreamClientTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private const string AckSuccess = """{"type":"ack","ackId":1,"success":true}""";

    private static (GameStreamClient Sut, FakeTimeProvider Time, Mock<IGameApiClient> Api, Mock<IAccessTokenProvider> Tokens)
        Build(FakeWebSocketConnectionFactory factory)
    {
        var api = new Mock<IGameApiClient>();
        api.Setup(a => a.GetNotificationsTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NotificationsTokenResult.Success("wss://hub.example.com/client?access_token=abc"));

        var tokens = new Mock<IAccessTokenProvider>();
        tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("access");

        var time = new FakeTimeProvider();
        var sut = new GameStreamClient(api.Object, tokens.Object, factory, time, NullLogger<GameStreamClient>.Instance);
        return (sut, time, api, tokens);
    }

    private static string GroupMessage(string eventType, string dataJson) =>
        "{\"type\":\"message\",\"from\":\"group\",\"data\":{\"type\":\"" + eventType + "\",\"data\":" + dataJson + "}}";

    private static async Task WaitFor(Func<bool> condition, string because)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.Elapsed < Timeout)
            await Task.Delay(10);
        Assert.True(condition(), because);
    }

    private static (List<GameStreamEvent> Events, CancellationTokenSource Cts, Task Consumer) Consume(
        GameStreamClient sut, Guid gameId)
    {
        var events = new List<GameStreamEvent>();
        var cts = new CancellationTokenSource();
        var consumer = Task.Run(async () =>
        {
            await foreach (var evt in sut.Subscribe(gameId, "access", cts.Token))
            {
                lock (events) { events.Add(evt); }
            }
        });
        return (events, cts, consumer);
    }

    private static int Count(List<GameStreamEvent> events)
    {
        lock (events) { return events.Count; }
    }

    [Fact]
    public async Task Subscribe_RequestsConnectionUrl_OpensSocket_JoinsGroup()
    {
        var gameId = Guid.NewGuid();
        var socket = new FakeWebSocketConnection();
        var (sut, _, api, _) = Build(new FakeWebSocketConnectionFactory(socket));
        var (_, cts, consumer) = Consume(sut, gameId);

        await socket.Opened.WaitAsync(Timeout);
        await WaitFor(() => socket.SentSnapshot().Any(m => m.Contains("joinGroup")), "a joinGroup frame is sent");

        Assert.Equal("json.webpubsub.azure.v1", socket.SubProtocol);
        Assert.Contains(socket.SentSnapshot(), m => m.Contains("joinGroup") && m.Contains(gameId.ToString()));
        api.Verify(a => a.GetNotificationsTokenAsync(gameId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        cts.Cancel();
        await consumer.WaitAsync(Timeout);
    }

    [Fact]
    public async Task Subscribe_MapsEachGroupEnvelope_ToTypedEvent()
    {
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var socket = new FakeWebSocketConnection();
        var (sut, _, _, _) = Build(new FakeWebSocketConnectionFactory(socket));
        var (events, cts, consumer) = Consume(sut, gameId);

        await socket.Opened.WaitAsync(Timeout);
        socket.Push(AckSuccess);
        socket.Push(GroupMessage("player-location-updated",
            $$"""{"gameId":"{{gameId}}","userId":"{{userId}}","latitude":52.1,"longitude":4.2,"participantState":"Active"}"""));
        socket.Push(GroupMessage("participant-status-changed",
            $$"""{"gameId":"{{gameId}}","participantId":"{{participantId}}","newState":"Tagged"}"""));
        socket.Push(GroupMessage("state-changed", $$"""{"gameId":"{{gameId}}","newState":"InProgress"}"""));
        socket.Push(GroupMessage("player-penalized", $$"""{"gameId":"{{gameId}}"}""")); // unconsumed → ignored
        socket.Push(GroupMessage("game-ended", $$"""{"gameId":"{{gameId}}","outcome":"HunterWins","survivorCount":0}"""));

        await WaitFor(() => Count(events) >= 4, "four consumed events arrive");

        lock (events)
        {
            Assert.Equal(userId, Assert.IsType<GameStreamEvent.ParticipantLocated>(events[0]).UserId);
            Assert.Equal(participantId, Assert.IsType<GameStreamEvent.ParticipantStatusChanged>(events[1]).ParticipantId);
            Assert.Equal("InProgress", Assert.IsType<GameStreamEvent.StateChanged>(events[2]).NewState);
            Assert.Equal("HunterWins", Assert.IsType<GameStreamEvent.GameEnded>(events[3]).Outcome);
        }

        cts.Cancel();
        await consumer.WaitAsync(Timeout);
    }

    [Fact]
    public async Task Subscribe_ReconnectsOnDrop_RefetchingToken()
    {
        var gameId = Guid.NewGuid();
        var s1 = new FakeWebSocketConnection();
        var s2 = new FakeWebSocketConnection();
        var (sut, time, api, _) = Build(new FakeWebSocketConnectionFactory(s1, s2));
        var (_, cts, consumer) = Consume(sut, gameId);

        await s1.Opened.WaitAsync(Timeout);
        s1.Push(AckSuccess);
        s1.DropConnection();

        // Fire the backoff timer until the second socket connects.
        var sw = Stopwatch.StartNew();
        while (!s2.ConnectCalled && sw.Elapsed < Timeout)
        {
            time.Advance(TimeSpan.FromSeconds(30));
            await Task.Delay(20);
        }

        Assert.True(s2.ConnectCalled, "the client reconnects on an unexpected drop");
        api.Verify(a => a.GetNotificationsTokenAsync(gameId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));

        cts.Cancel();
        await consumer.WaitAsync(Timeout);
    }

    [Fact]
    public async Task Subscribe_ForbiddenToken_EndsEnumerationWithoutOpeningSocket()
    {
        var gameId = Guid.NewGuid();
        var factory = new FakeWebSocketConnectionFactory();
        var (sut, _, api, _) = Build(factory);
        api.Setup(a => a.GetNotificationsTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NotificationsTokenResult.Forbidden);
        var (_, _, consumer) = Consume(sut, gameId);

        await consumer.WaitAsync(Timeout);

        Assert.Empty(factory.Created);
    }

    [Fact]
    public async Task Subscribe_Cancellation_CompletesEnumeration_AndClosesSocket()
    {
        var gameId = Guid.NewGuid();
        var socket = new FakeWebSocketConnection();
        var (sut, _, _, _) = Build(new FakeWebSocketConnectionFactory(socket));
        var (_, cts, consumer) = Consume(sut, gameId);

        await socket.Opened.WaitAsync(Timeout);
        socket.Push(AckSuccess);

        cts.Cancel();
        await consumer.WaitAsync(Timeout);

        Assert.True(socket.CloseCalled);
    }
}
