using System.Text.Json;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Realtime;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class GameStateServiceTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly FakeGameRealtimeConnection _connection = new();
    private readonly Mock<IGameApiClient> _gameApi = new();
    private readonly Mock<IAccessTokenProvider> _tokenProvider = new();

    private GameStateService CreateSut() =>
        new(_connection, _gameApi.Object, _tokenProvider.Object, NullLogger<GameStateService>.Instance);

    // ---- Helpers ----

    private static GameConfigurationDetails Config() => new(30, 5, 10, 120, 60);

    private static GameParticipantDetails Participant(Guid id, string name = "Alice", string state = "Active") =>
        new(id, name, IsReady: true, State: state);

    private static GameDetails Seed(
        Guid? id = null, string status = "InProgress", params GameParticipantDetails[] participants)
    {
        var list = participants.Length > 0
            ? participants
            : new[] { Participant(Guid.NewGuid()) };
        return new GameDetails(
            id ?? Guid.NewGuid(), "1234", status, Config(), list,
            HunterUserId: null, OwnerUserId: Guid.NewGuid(), IsOwnerPlayer: true, IsReadyToStart: false);
    }

    private static GameRealtimeEnvelope Envelope(string type, object? data)
    {
        var json = data is null ? "null" : JsonSerializer.Serialize(data, WebJson);
        using var doc = JsonDocument.Parse(json);
        return new GameRealtimeEnvelope(type, doc.RootElement.Clone());
    }

    // Seeds state through a full-snapshot event, then clears any recorded broadcast.
    private GameStateService StartedWith(GameDetails seed)
    {
        var sut = CreateSut();
        _connection.RaiseEnvelope(Envelope("lobby-updated", seed));
        return sut;
    }

    // ---- 5.2 reconcile on (re)connect ----

    [Fact]
    public void Connected_ShouldFetchSnapshot_AdoptState_AndBroadcast()
    {
        var game = Seed(status: "InProgress");
        _tokenProvider.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("access");
        _gameApi.Setup(a => a.GetGameAsync(game.Id, "access", It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetGameResult.Success(game));

        var sut = CreateSut();
        GameStateChanged? received = null;
        sut.Subscribe(c => received = c);

        sut.Start(game.Id);
        _connection.RaiseConnected();

        Assert.Equal(game.Id, sut.CurrentState!.Id);
        Assert.Equal(game.Id, received!.State.Id);
    }

    [Fact]
    public void Reconnected_ShouldReconcileFreshSnapshot()
    {
        var gameId = Guid.NewGuid();
        var refreshed = Seed(gameId, status: "Endgame");
        _tokenProvider.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("access");
        _gameApi.Setup(a => a.GetGameAsync(gameId, "access", It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetGameResult.Success(refreshed));

        var sut = CreateSut();
        sut.Start(gameId);
        _connection.RaiseReconnected();

        Assert.Equal("Endgame", sut.CurrentState!.Status);
    }

    [Fact]
    public void Connected_ShouldNotOverwriteState_WhenSnapshotFetchFails()
    {
        var gameId = Guid.NewGuid();
        _tokenProvider.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("access");
        _gameApi.Setup(a => a.GetGameAsync(gameId, "access", It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetGameResult.Error);

        var sut = CreateSut();
        sut.Start(gameId);
        _connection.RaiseConnected();

        Assert.Null(sut.CurrentState);
    }

    // ---- 7.1 event application ----

    [Fact]
    public void FullSnapshotEvent_ShouldReplaceState()
    {
        var game = Seed(status: "Lobby");
        var sut = CreateSut();

        _connection.RaiseEnvelope(Envelope("lobby-updated", game));

        Assert.Equal(game.Id, sut.CurrentState!.Id);
        Assert.Equal("Lobby", sut.CurrentState.Status);
    }

    [Fact]
    public void StateChanged_ShouldUpdateStatus_AndPreserveParticipants()
    {
        var participant = Participant(Guid.NewGuid(), "Bob");
        var game = Seed(null, "InProgress", participant);
        var sut = StartedWith(game);

        _connection.RaiseEnvelope(Envelope(
            "state-changed", new StateChangedPayload(game.Id, "Endgame")));

        Assert.Equal("Endgame", sut.CurrentState!.Status);
        Assert.Single(sut.CurrentState.Participants);
        Assert.Equal("Bob", sut.CurrentState.Participants[0].DisplayName);
    }

    [Fact]
    public void PlayerLocationUpdated_ShouldUpdateOnlyTargetParticipant()
    {
        var target = Participant(Guid.NewGuid(), "Target");
        var other = Participant(Guid.NewGuid(), "Other");
        var game = Seed(null, "InProgress", target, other);
        var sut = StartedWith(game);

        _connection.RaiseEnvelope(Envelope("player-location-updated",
            new PlayerLocationUpdatedPayload(game.Id, target.UserId, 51.5, 4.5, "Active")));

        var updatedTarget = sut.CurrentState!.Participants.Single(p => p.UserId == target.UserId);
        var untouched = sut.CurrentState.Participants.Single(p => p.UserId == other.UserId);
        Assert.Equal(51.5, updatedTarget.Latitude);
        Assert.Equal(4.5, updatedTarget.Longitude);
        Assert.Null(untouched.Latitude);
        Assert.Null(untouched.Longitude);
    }

    [Fact]
    public void ParticipantStatusChanged_ShouldUpdateParticipantState()
    {
        var target = Participant(Guid.NewGuid(), "Target", state: "Active");
        var game = Seed(null, "InProgress", target, Participant(Guid.NewGuid(), "Other"));
        var sut = StartedWith(game);

        _connection.RaiseEnvelope(Envelope("participant-status-changed",
            new ParticipantStatusChangedPayload(game.Id, target.UserId, "Prey", "Tagged")));

        Assert.Equal("Tagged", sut.CurrentState!.Participants.Single(p => p.UserId == target.UserId).State);
    }

    [Fact]
    public void PlayerPenalized_ShouldRecordPenaltyOnParticipant()
    {
        var target = Participant(Guid.NewGuid(), "Target");
        var game = Seed(null, "InProgress", target);
        var sut = StartedWith(game);
        var ends = DateTimeOffset.Parse("2026-07-15T10:00:00Z");

        _connection.RaiseEnvelope(Envelope("player-penalized",
            new PlayerPenalizedPayload(game.Id, target.UserId, ends, "OutOfBounds")));

        Assert.Equal(ends, sut.CurrentState!.Participants.Single(p => p.UserId == target.UserId).PenaltyEndsAt);
    }

    [Fact]
    public void GameEnded_ShouldMarkStateCompleted()
    {
        var game = Seed(null, "InProgress");
        var sut = StartedWith(game);

        _connection.RaiseEnvelope(Envelope("game-ended",
            new GameEndedPayload(game.Id, "PreysWin", 2)));

        Assert.Equal("Completed", sut.CurrentState!.Status);
    }

    // ---- 7.2 unknown / malformed ----

    [Fact]
    public void UnknownEventType_ShouldNotChangeStateNorBroadcast()
    {
        var game = Seed(null, "InProgress");
        var sut = StartedWith(game);
        var before = sut.CurrentState;
        var broadcasts = 0;
        sut.Subscribe(_ => broadcasts++);

        _connection.RaiseEnvelope(Envelope("totally-unknown", new { anything = 1 }));

        Assert.Same(before, sut.CurrentState);
        Assert.Equal(0, broadcasts);
    }

    [Fact]
    public void EnvelopeWithEmptyType_ShouldBeIgnored()
    {
        var game = Seed(null, "InProgress");
        var sut = StartedWith(game);
        var before = sut.CurrentState;

        _connection.RaiseEnvelope(Envelope("", new { }));

        Assert.Same(before, sut.CurrentState);
    }

    [Fact]
    public void MalformedDeltaPayload_ShouldBeIgnored()
    {
        var game = Seed(null, "InProgress");
        var sut = StartedWith(game);
        var before = sut.CurrentState;
        var broadcasts = 0;
        sut.Subscribe(_ => broadcasts++);

        // state-changed with no 'newState' → NewState null → no-op.
        _connection.RaiseEnvelope(Envelope("state-changed", new { irrelevant = "x" }));
        // a delta whose data is a bare string → deserialization fails → no-op.
        _connection.RaiseEnvelope(Envelope("participant-status-changed", "not-an-object"));

        Assert.Same(before, sut.CurrentState);
        Assert.Equal(0, broadcasts);
    }

    [Fact]
    public void LocationForUnknownParticipant_ShouldBeNoOp()
    {
        var game = Seed(null, "InProgress", Participant(Guid.NewGuid(), "Known"));
        var sut = StartedWith(game);
        var before = sut.CurrentState;
        var broadcasts = 0;
        sut.Subscribe(_ => broadcasts++);

        _connection.RaiseEnvelope(Envelope("player-location-updated",
            new PlayerLocationUpdatedPayload(game.Id, Guid.NewGuid(), 1, 2, "Active")));

        Assert.Same(before, sut.CurrentState);
        Assert.Equal(0, broadcasts);
    }

    [Fact]
    public void DeltaBeforeAnySnapshot_ShouldBeNoOp()
    {
        var sut = CreateSut();

        _connection.RaiseEnvelope(Envelope("state-changed", new StateChangedPayload(Guid.NewGuid(), "Endgame")));

        Assert.Null(sut.CurrentState);
    }

    // ---- 7.3 notifications ----

    [Fact]
    public void Subscribers_ShouldEachReceiveCurrentState_OnChange()
    {
        var game = Seed(null, "InProgress");
        var sut = StartedWith(game);
        GameStateChanged? a = null, b = null;
        sut.Subscribe(c => a = c);
        sut.Subscribe(c => b = c);

        _connection.RaiseEnvelope(Envelope("state-changed", new StateChangedPayload(game.Id, "Endgame")));

        Assert.Equal("Endgame", a!.State.Status);
        Assert.Equal("Endgame", b!.State.Status);
    }

    [Fact]
    public void ThrowingSubscriber_ShouldNotStarveOtherSubscribers()
    {
        var game = Seed(null, "InProgress");
        var sut = StartedWith(game);
        var secondCalled = false;
        sut.Subscribe(_ => throw new InvalidOperationException("boom"));
        sut.Subscribe(_ => secondCalled = true);

        var ex = Record.Exception(() =>
            _connection.RaiseEnvelope(Envelope("state-changed", new StateChangedPayload(game.Id, "Endgame"))));

        Assert.Null(ex);
        Assert.True(secondCalled);
    }

    [Fact]
    public void UnsubscribedHandler_ShouldStopReceiving()
    {
        var game = Seed(null, "InProgress");
        var sut = StartedWith(game);
        var count = 0;
        void Handler(GameStateChanged _) => count++;
        sut.Subscribe(Handler);

        _connection.RaiseEnvelope(Envelope("state-changed", new StateChangedPayload(game.Id, "Endgame")));
        sut.Unsubscribe(Handler);
        _connection.RaiseEnvelope(Envelope("state-changed", new StateChangedPayload(game.Id, "InProgress")));

        Assert.Equal(1, count);
    }

    // ---- lifecycle passthrough ----

    [Fact]
    public void Start_ShouldStartUnderlyingConnection_AndStopStopsIt()
    {
        var gameId = Guid.NewGuid();
        var sut = CreateSut();

        sut.Start(gameId);
        Assert.Equal(gameId, _connection.StartedGameId);

        _ = sut.StopAsync();
        Assert.True(_connection.Stopped);
    }
}

/// <summary>Hand-driven <see cref="IGameRealtimeConnection"/> so tests can raise events synchronously.</summary>
internal sealed class FakeGameRealtimeConnection : IGameRealtimeConnection
{
    public event Action<GameRealtimeEnvelope>? EnvelopeReceived;
    public event Action? Connected;
    public event Action? Reconnected;
    public event Action? Unavailable;

    public Guid? StartedGameId { get; private set; }
    public bool Stopped { get; private set; }

    public void Start(Guid gameId) => StartedGameId = gameId;
    public Task StopAsync() { Stopped = true; return Task.CompletedTask; }

    public void RaiseEnvelope(GameRealtimeEnvelope envelope) => EnvelopeReceived?.Invoke(envelope);
    public void RaiseConnected() => Connected?.Invoke();
    public void RaiseReconnected() => Reconnected?.Invoke();
    public void RaiseUnavailable() => Unavailable?.Invoke();
}
