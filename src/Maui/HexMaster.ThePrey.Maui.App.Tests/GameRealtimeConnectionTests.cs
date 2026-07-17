using System.Diagnostics;
using System.Threading.Channels;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Realtime;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class GameRealtimeConnectionTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static (GameRealtimeConnection Sut, FakeWebSocketConnectionFactory Factory, FakeTimeProvider Time,
        Mock<IGameApiClient> Api, Mock<IAccessTokenProvider> Tokens) Build(params FakeWebSocketConnection[] sockets)
    {
        var api = new Mock<IGameApiClient>();
        api.Setup(a => a.GetNotificationsTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NotificationsTokenResult.Success("wss://hub.example.com/client?access_token=abc"));

        var tokens = new Mock<IAccessTokenProvider>();
        tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("access");

        var factory = new FakeWebSocketConnectionFactory(sockets);
        var time = new FakeTimeProvider();
        var sut = new GameRealtimeConnection(api.Object, tokens.Object, factory, time, NullLogger<GameRealtimeConnection>.Instance);
        return (sut, factory, time, api, tokens);
    }

    private const string AckSuccess = """{"type":"ack","ackId":1,"success":true}""";
    private const string AckDuplicate = """{"type":"ack","ackId":1,"success":false,"error":{"name":"Duplicate"}}""";

    // Mirrors the canonical envelope { v, type, gameId, seq, data } from docs/api/realtime.md.
    private static string GroupMessage(string eventType, int v = 1, long seq = 1, Guid? gameId = null) =>
        "{\"type\":\"message\",\"from\":\"group\",\"data\":{\"v\":" + v + ",\"type\":\"" + eventType +
        "\",\"gameId\":\"" + (gameId ?? Guid.NewGuid()) + "\",\"seq\":" + seq + ",\"data\":{}}}";

    // Advances the fake clock in coarse steps until a condition holds (fires whatever backoff timer is pending).
    private static async Task AdvanceUntilAsync(Func<bool> condition, FakeTimeProvider time)
    {
        var deadline = Stopwatch.GetTimestamp() + (long)(Timeout.TotalSeconds * Stopwatch.Frequency);
        while (!condition())
        {
            time.Advance(TimeSpan.FromSeconds(30));
            await Task.Delay(20);
            if (Stopwatch.GetTimestamp() > deadline)
                throw new TimeoutException("Condition was not met within the timeout.");
        }
    }

    // ---- 7.4 connect / join / lifecycle ----

    [Fact]
    public async Task Start_ShouldOpenSocket_JoinGroup_WithJsonSubprotocol()
    {
        var gameId = Guid.NewGuid();
        var socket = new FakeWebSocketConnection();
        var (sut, _, _, _, _) = Build(socket);

        sut.Start(gameId);
        await socket.Opened.WaitAsync(Timeout);

        Assert.Equal("json.webpubsub.azure.v1", socket.SubProtocol);
        var sent = socket.SentSnapshot();
        Assert.Contains(sent, m => m.Contains("joinGroup") && m.Contains(gameId.ToString()));

        await sut.StopAsync();
    }

    [Fact]
    public async Task SuccessfulAck_ShouldRaiseConnected()
    {
        var socket = new FakeWebSocketConnection();
        var (sut, _, _, _, _) = Build(socket);
        var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sut.Connected += () => connected.TrySetResult();

        sut.Start(Guid.NewGuid());
        await socket.Opened.WaitAsync(Timeout);
        socket.Push(AckSuccess);

        await connected.Task.WaitAsync(Timeout);
        await sut.StopAsync();
    }

    [Fact]
    public async Task DuplicateAck_ShouldAlsoRaiseConnected()
    {
        var socket = new FakeWebSocketConnection();
        var (sut, _, _, _, _) = Build(socket);
        var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sut.Connected += () => connected.TrySetResult();

        sut.Start(Guid.NewGuid());
        await socket.Opened.WaitAsync(Timeout);
        socket.Push(AckDuplicate);

        await connected.Task.WaitAsync(Timeout);
        await sut.StopAsync();
    }

    [Fact]
    public async Task GroupMessage_ShouldRaiseEnvelope()
    {
        var socket = new FakeWebSocketConnection();
        var (sut, _, _, _, _) = Build(socket);
        var envelope = new TaskCompletionSource<GameRealtimeEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        sut.EnvelopeReceived += e => envelope.TrySetResult(e);

        sut.Start(Guid.NewGuid());
        await socket.Opened.WaitAsync(Timeout);
        socket.Push(AckSuccess);
        socket.Push(GroupMessage("configuration-changed"));

        var received = await envelope.Task.WaitAsync(Timeout);
        Assert.Equal("configuration-changed", received.Type);
        await sut.StopAsync();
    }

    [Fact]
    public async Task GroupMessage_ShouldParseVersionSeqAndGameId()
    {
        var gameId = Guid.NewGuid();
        var socket = new FakeWebSocketConnection();
        var (sut, _, _, _, _) = Build(socket);
        var envelope = new TaskCompletionSource<GameRealtimeEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        sut.EnvelopeReceived += e => envelope.TrySetResult(e);

        sut.Start(gameId);
        await socket.Opened.WaitAsync(Timeout);
        socket.Push(AckSuccess);
        socket.Push(GroupMessage("prey-updated", v: 1, seq: 42, gameId: gameId));

        var received = await envelope.Task.WaitAsync(Timeout);
        Assert.Equal("prey-updated", received.Type);
        Assert.Equal(1, received.Version);
        Assert.Equal(42, received.Seq);
        Assert.Equal(gameId, received.GameId);
        await sut.StopAsync();
    }

    [Fact]
    public async Task Start_ShouldBeIdempotent_NoSecondSocket()
    {
        var socket = new FakeWebSocketConnection();
        var (sut, factory, _, _, _) = Build(socket);
        var gameId = Guid.NewGuid();

        sut.Start(gameId);
        await socket.Opened.WaitAsync(Timeout);
        sut.Start(gameId); // second call is a no-op

        Assert.Single(factory.Created);
        await sut.StopAsync();
    }

    [Fact]
    public async Task StopAsync_ShouldCloseSocket()
    {
        var socket = new FakeWebSocketConnection();
        var (sut, _, _, _, _) = Build(socket);
        var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sut.Connected += () => connected.TrySetResult();

        sut.Start(Guid.NewGuid());
        await socket.Opened.WaitAsync(Timeout);
        socket.Push(AckSuccess);
        await connected.Task.WaitAsync(Timeout);

        await sut.StopAsync();

        Assert.True(socket.CloseCalled);
    }

    // ---- 7.5 reconnect / reconcile / backoff / terminal denial ----

    [Fact]
    public async Task TerminalForbidden_ShouldRaiseUnavailable_AndNotOpenSocket()
    {
        var (sut, factory, _, api, _) = Build();
        api.Setup(a => a.GetNotificationsTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NotificationsTokenResult.Forbidden);
        var unavailable = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sut.Unavailable += () => unavailable.TrySetResult();

        sut.Start(Guid.NewGuid());

        await unavailable.Task.WaitAsync(Timeout);
        Assert.Empty(factory.Created);
        await sut.StopAsync();
    }

    [Fact]
    public async Task UnexpectedDrop_ShouldReconnect_RefetchToken_AndRaiseReconnected()
    {
        var s1 = new FakeWebSocketConnection();
        var s2 = new FakeWebSocketConnection();
        var (sut, _, time, api, _) = Build(s1, s2);
        var reconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sut.Reconnected += () => reconnected.TrySetResult();

        sut.Start(Guid.NewGuid());
        await s1.Opened.WaitAsync(Timeout);
        s1.Push(AckSuccess);          // first join
        s1.DropConnection();          // simulate the socket closing

        await AdvanceUntilAsync(() => s2.ConnectCalled, time);
        s2.Push(AckSuccess);          // re-join on the new socket

        await reconnected.Task.WaitAsync(Timeout);
        api.Verify(a => a.GetNotificationsTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(2));
        await sut.StopAsync();
    }

    [Fact]
    public async Task TransientTokenFailure_ShouldRetry_UntilSuccess()
    {
        var socket = new FakeWebSocketConnection();
        var (sut, factory, time, api, _) = Build(socket);
        api.SetupSequence(a => a.GetNotificationsTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NotificationsTokenResult.Error)
            .ReturnsAsync(NotificationsTokenResult.Success("wss://hub.example.com/client?access_token=abc"));

        sut.Start(Guid.NewGuid());

        await AdvanceUntilAsync(() => socket.ConnectCalled, time);
        api.Verify(a => a.GetNotificationsTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(2));
        await sut.StopAsync();
    }

    [Theory]
    [InlineData(1, 1)]    // first attempt → the minimum
    [InlineData(2, 2)]    // 1 * 2^1
    [InlineData(3, 4)]    // 1 * 2^2
    [InlineData(5, 16)]   // 1 * 2^4
    [InlineData(6, 30)]   // 32 clamps to max
    [InlineData(100, 30)] // stays clamped, no overflow
    public void ComputeBackoff_ShouldGrowExponentially_AndClampToMax(int attempt, int expectedSeconds)
    {
        var result = GameRealtimeConnection.ComputeBackoff(attempt, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), result);
    }
}

/// <summary>
/// Scriptable <see cref="IWebSocketConnection"/>. <see cref="Push"/> queues an inbound frame;
/// <see cref="DropConnection"/> makes the next receive return <c>null</c> (a socket close).
/// </summary>
internal sealed class FakeWebSocketConnection : IWebSocketConnection
{
    private readonly Channel<string> _incoming = Channel.CreateUnbounded<string>();
    private readonly List<string> _sent = new();
    private readonly TaskCompletionSource _opened = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Opened => _opened.Task;
    public bool ConnectCalled { get; private set; }
    public bool CloseCalled { get; private set; }
    public string? SubProtocol { get; private set; }

    public Task ConnectAsync(Uri uri, string subProtocol, CancellationToken ct)
    {
        ConnectCalled = true;
        SubProtocol = subProtocol;
        _opened.TrySetResult();
        return Task.CompletedTask;
    }

    public Task SendTextAsync(string message, CancellationToken ct)
    {
        lock (_sent) { _sent.Add(message); }
        return Task.CompletedTask;
    }

    public async Task<string?> ReceiveTextAsync(CancellationToken ct)
    {
        try
        {
            return await _incoming.Reader.ReadAsync(ct);
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }

    public Task CloseAsync(CancellationToken ct)
    {
        CloseCalled = true;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void Push(string message) => _incoming.Writer.TryWrite(message);

    public void DropConnection() => _incoming.Writer.TryComplete();

    public string[] SentSnapshot()
    {
        lock (_sent) { return _sent.ToArray(); }
    }
}

internal sealed class FakeWebSocketConnectionFactory : IWebSocketConnectionFactory
{
    private readonly Queue<FakeWebSocketConnection> _queued;
    private readonly List<FakeWebSocketConnection> _created = new();
    private readonly object _gate = new();

    public FakeWebSocketConnectionFactory(params FakeWebSocketConnection[] sockets) => _queued = new(sockets);

    public IReadOnlyList<FakeWebSocketConnection> Created
    {
        get { lock (_gate) { return _created.ToArray(); } }
    }

    public IWebSocketConnection Create()
    {
        lock (_gate)
        {
            var socket = _queued.Count > 0 ? _queued.Dequeue() : new FakeWebSocketConnection();
            _created.Add(socket);
            return socket;
        }
    }
}
