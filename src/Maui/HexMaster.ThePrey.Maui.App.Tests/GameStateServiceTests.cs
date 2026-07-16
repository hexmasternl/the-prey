using System.Text.Json;
using HexMaster.ThePrey.Maui.App.Services.Api;
using HexMaster.ThePrey.Maui.App.Services.Authentication;
using HexMaster.ThePrey.Maui.App.Services.Realtime;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace HexMaster.ThePrey.Maui.App.Tests;

public class GameStateServiceTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly FakeGameRealtimeConnection _connection = new();
    private readonly Mock<IGameApiClient> _gameApi = new();
    private readonly Mock<IAccessTokenProvider> _tokenProvider = new();
    private readonly FakeTimeProvider _time = new();

    private GameStateService CreateSut() =>
        new(_connection, _gameApi.Object, _tokenProvider.Object, _time, NullLogger<GameStateService>.Instance);

    // ---- Helpers ----

    private static GameConfigurationDetails Config() => new(30, 5, 10, 120, 60);

    private static GameParticipantDetails Participant(Guid id, string name = "Alice", string state = "Active") =>
        new(id, name, IsReady: true, State: state);

    private static GameDetails Seed(
        Guid? id = null, string status = "InProgress", Guid? hunterUserId = null, params GameParticipantDetails[] participants)
    {
        var list = participants.Length > 0
            ? participants
            : new[] { Participant(Guid.NewGuid()) };
        return new GameDetails(
            id ?? Guid.NewGuid(), "1234", status, Config(), list,
            HunterUserId: hunterUserId, OwnerUserId: Guid.NewGuid(), IsOwnerPlayer: true, IsReadyToStart: false);
    }

    private static GameRealtimeEnvelope Envelope(string type, object? data)
    {
        var json = data is null ? "null" : JsonSerializer.Serialize(data, WebJson);
        using var doc = JsonDocument.Parse(json);
        return new GameRealtimeEnvelope(type, doc.RootElement.Clone());
    }

    // Sets up a full successful reconcile: token + game record, plus (optionally) the in-progress status
    // details and role-specific state. Unmocked status/state reads default to "not available" so an
    // InProgress reconcile that only cares about the game record still completes without a null Task.
    private void SetupReconcile(GameDetails game, GameStatusDetails? details = null, GameStateSnapshot? state = null)
    {
        _tokenProvider.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("access");
        _gameApi.Setup(a => a.GetGameAsync(game.Id, "access", It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetGameResult.Success(game));
        _gameApi.Setup(a => a.GetGameStatusDetailsAsync(game.Id, "access", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details is null ? GetGameStatusResult.Forbidden : GetGameStatusResult.Success(details));
        _gameApi.Setup(a => a.GetGameStateAsync(game.Id, "access", It.IsAny<CancellationToken>()))
            .ReturnsAsync(state is null ? GameStateResult.NotFound : GameStateResult.Success(state));
    }

    // Seeds state through a full-snapshot event (the way a lobby/game-started broadcast arrives).
    private GameStateService StartedWith(GameDetails seed)
    {
        var sut = CreateSut();
        _connection.RaiseEnvelope(Envelope("lobby-updated", seed));
        return sut;
    }

    // ---- reconcile on (re)connect ----

    [Fact]
    public void Connected_ShouldFetchSnapshot_AdoptState_AndBroadcast()
    {
        var game = Seed(status: "InProgress");
        SetupReconcile(game);

        var sut = CreateSut();
        GameStateChanged? received = null;
        sut.Subscribe(c => received = c);

        sut.Start(game.Id);
        _connection.RaiseConnected();

        Assert.Equal(game.Id, sut.CurrentState!.GameId);
        Assert.Equal(game.Id, received!.State.GameId);
    }

    [Fact]
    public void Reconnected_ShouldReconcileFreshSnapshot()
    {
        var gameId = Guid.NewGuid();
        var refreshed = Seed(gameId, status: "Completed");
        SetupReconcile(refreshed);

        var sut = CreateSut();
        sut.Start(gameId);
        _connection.RaiseReconnected();

        Assert.Equal("Completed", sut.CurrentState!.Status);
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

    [Fact]
    public void Reconcile_ShouldFoldStatusAndStateIntoTheComposite()
    {
        var hunterId = Guid.NewGuid();
        var preyId = Guid.NewGuid();
        var game = Seed(null, "InProgress", hunterId,
            Participant(hunterId, "Hunter"), Participant(preyId, "Prey"));
        var details = new GameStatusDetails(
            PlayfieldCoordinates: new[] { new GpsCoordinate(51.0, 4.0), new GpsCoordinate(51.1, 4.1), new GpsCoordinate(51.2, 4.0) },
            Participants: new[]
            {
                new GameParticipantStatusDetails(hunterId, new GpsCoordinate(51.05, 4.05), "Active"),
                new GameParticipantStatusDetails(preyId, new GpsCoordinate(51.06, 4.06), "Active"),
            },
            HunterUserId: hunterId,
            GameDurationLeft: 900,
            HunterMayMoveAt: null,
            IsEndgame: false,
            PreysLeft: 1,
            NextPingDuration: 20,
            CurrentPingInterval: 30);
        var state = new GameStateSnapshot(HunterDistanceMeters: 42, PreyLocations: new[] { new GpsCoordinate(51.06, 4.06) });
        SetupReconcile(game, details, state);

        var sut = CreateSut();
        sut.Start(game.Id);
        _connection.RaiseConnected();

        var current = sut.CurrentState!;
        Assert.Equal(3, current.PlayfieldCoordinates.Count);
        Assert.Equal(900, current.GameDurationLeft);
        Assert.Equal(20, current.NextPingDuration);
        Assert.Equal(30, current.CurrentPingInterval);
        Assert.Equal(42, current.HunterDistanceMeters);
        var prey = current.Participants.Single(p => p.UserId == preyId);
        Assert.Equal(51.06, prey.Location!.Latitude);
    }

    [Fact]
    public void PeriodicHeartbeat_ShouldReconcileEveryFiveMinutes()
    {
        var gameId = Guid.NewGuid();
        SetupReconcile(Seed(gameId, status: "Completed"));

        var sut = CreateSut();
        sut.Start(gameId);
        Assert.Null(sut.CurrentState); // Start does not reconcile immediately.

        _time.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal("Completed", sut.CurrentState!.Status);
        _gameApi.Verify(a => a.GetGameAsync(gameId, "access", It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    // ---- StartAsync (resolve active game) ----

    [Fact]
    public async Task StartAsync_ShouldResolveActiveGame_SeedState_AndStartConnection()
    {
        var gameId = Guid.NewGuid();
        _tokenProvider.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("access");
        _gameApi.Setup(a => a.GetActiveGameAsync("access", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveGameResult.Active(new GameStatus { GameId = gameId }));
        SetupReconcile(Seed(gameId, status: "InProgress"));

        var sut = CreateSut();
        var seeded = await sut.StartAsync();

        Assert.NotNull(seeded);
        Assert.Equal(gameId, seeded!.GameId);
        Assert.Equal(gameId, _connection.StartedGameId);
    }

    [Fact]
    public async Task StartAsync_ShouldReturnNull_WhenNoActiveGame()
    {
        _tokenProvider.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("access");
        _gameApi.Setup(a => a.GetActiveGameAsync("access", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveGameResult.None);

        var sut = CreateSut();
        var seeded = await sut.StartAsync();

        Assert.Null(seeded);
        Assert.Null(_connection.StartedGameId);
    }

    // ---- event application ----

    [Fact]
    public void FullSnapshotEvent_ShouldReplaceState()
    {
        var game = Seed(status: "Lobby");
        var sut = CreateSut();

        _connection.RaiseEnvelope(Envelope("lobby-updated", game));

        Assert.Equal(game.Id, sut.CurrentState!.GameId);
        Assert.Equal("Lobby", sut.CurrentState.Status);
    }

    [Fact]
    public void StateChanged_ShouldUpdateStatus_AndPreserveParticipants()
    {
        var participant = Participant(Guid.NewGuid(), "Bob");
        var game = Seed(null, "InProgress", null, participant);
        var sut = StartedWith(game);

        _connection.RaiseEnvelope(Envelope(
            "state-changed", new StateChangedPayload(game.Id, "Endgame")));

        Assert.Equal("Endgame", sut.CurrentState!.Status);
        Assert.Single(sut.CurrentState.Participants);
        Assert.Equal(participant.UserId, sut.CurrentState.Participants[0].UserId);
    }

    [Fact]
    public void PlayerLocationUpdated_ShouldUpdateOnlyTargetParticipant()
    {
        var target = Participant(Guid.NewGuid(), "Target");
        var other = Participant(Guid.NewGuid(), "Other");
        var game = Seed(null, "InProgress", null, target, other);
        var sut = StartedWith(game);

        _connection.RaiseEnvelope(Envelope("player-location-updated",
            new PlayerLocationUpdatedPayload(game.Id, target.UserId, 51.5, 4.5, "Active")));

        var updatedTarget = sut.CurrentState!.Participants.Single(p => p.UserId == target.UserId);
        var untouched = sut.CurrentState.Participants.Single(p => p.UserId == other.UserId);
        Assert.Equal(51.5, updatedTarget.Location!.Latitude);
        Assert.Equal(4.5, updatedTarget.Location.Longitude);
        Assert.Null(untouched.Location);
    }

    [Fact]
    public void ParticipantStatusChanged_ShouldUpdateState_AndRecomputePreysLeft()
    {
        var hunterId = Guid.NewGuid();
        var target = Participant(Guid.NewGuid(), "Target", state: "Active");
        var otherPrey = Participant(Guid.NewGuid(), "Other", state: "Active");
        var game = Seed(null, "InProgress", hunterId, Participant(hunterId, "Hunter"), target, otherPrey);
        var sut = StartedWith(game);
        Assert.Equal(2, sut.CurrentState!.PreysLeft);

        _connection.RaiseEnvelope(Envelope("participant-status-changed",
            new ParticipantStatusChangedPayload(game.Id, target.UserId, "Prey", "Tagged")));

        Assert.Equal("Tagged", sut.CurrentState!.Participants.Single(p => p.UserId == target.UserId).State);
        Assert.Equal(1, sut.CurrentState.PreysLeft);
    }

    [Fact]
    public void PlayerPenalized_ShouldRecordPenaltyOnParticipant()
    {
        var target = Participant(Guid.NewGuid(), "Target");
        var game = Seed(null, "InProgress", null, target);
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
        Assert.True(sut.CurrentState.IsCompleted);
    }

    // ---- unknown / malformed ----

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
        var game = Seed(null, "InProgress", null, Participant(Guid.NewGuid(), "Known"));
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

    // ---- notifications ----

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
