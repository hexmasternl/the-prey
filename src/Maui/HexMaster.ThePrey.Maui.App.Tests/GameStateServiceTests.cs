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

    private static GameParticipantDetails Participant(Guid id, string name = "Alice", string state = "Active", bool ready = true) =>
        new(id, name, ready, state);

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

    private static GameRealtimeEnvelope Envelope(string type, object? data, int? version = 1, long? seq = null)
    {
        var json = data is null ? "null" : JsonSerializer.Serialize(data, WebJson);
        using var doc = JsonDocument.Parse(json);
        return new GameRealtimeEnvelope(type, doc.RootElement.Clone(), version, seq);
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

    // Seeds state through the normal REST reconcile path (there are no more wire full-snapshot events —
    // every real-time message is a delta onto a snapshot obtained this way).
    private GameStateService StartedWith(GameDetails seed)
    {
        SetupReconcile(seed);
        var sut = CreateSut();
        sut.Start(seed.Id);
        _connection.RaiseConnected();
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
    public void PeriodicHeartbeat_ShouldReconcileEveryThreeMinutes()
    {
        var gameId = Guid.NewGuid();
        SetupReconcile(Seed(gameId, status: "Completed"));

        var sut = CreateSut();
        sut.Start(gameId);
        Assert.Null(sut.CurrentState); // Start does not reconcile immediately.

        _time.Advance(TimeSpan.FromMinutes(3));

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

    // ---- participant deltas ----

    [Fact]
    public void ParticipantJoined_ShouldAddParticipant_ToStateAndGame()
    {
        var existing = Participant(Guid.NewGuid(), "Existing");
        var game = Seed(null, "Lobby", null, existing);
        var sut = StartedWith(game);
        var newUserId = Guid.NewGuid();

        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.ParticipantJoined,
            new ParticipantPayload(newUserId, "Newcomer", IsReady: false, State: "Active", LastKnownLocation: null, HasActivePenalty: false),
            seq: 1));

        Assert.Equal(2, sut.CurrentState!.Participants.Count);
        Assert.Contains(sut.CurrentState.Participants, p => p.UserId == newUserId);
        Assert.Equal(2, sut.CurrentGame!.Participants.Count);
        var added = sut.CurrentGame.Participants.Single(p => p.UserId == newUserId);
        Assert.Equal("Newcomer", added.DisplayName);
        Assert.False(added.IsReady);
    }

    [Fact]
    public void ParticipantChanged_ShouldReplaceThatParticipant_AndPreserveOthers()
    {
        var target = Participant(Guid.NewGuid(), "Target", ready: false);
        var other = Participant(Guid.NewGuid(), "Other");
        var game = Seed(null, "Lobby", null, target, other);
        var sut = StartedWith(game);

        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.ParticipantChanged,
            new ParticipantPayload(target.UserId, "Target", IsReady: true, State: "Active", LastKnownLocation: null, HasActivePenalty: false),
            seq: 1));

        Assert.True(sut.CurrentGame!.Participants.Single(p => p.UserId == target.UserId).IsReady);
        Assert.Equal(2, sut.CurrentGame.Participants.Count);
        Assert.Equal(2, sut.CurrentState!.Participants.Count);
        // The other participant is untouched.
        Assert.Equal(other.IsReady, sut.CurrentGame.Participants.Single(p => p.UserId == other.UserId).IsReady);
    }

    [Fact]
    public void ParticipantChanged_ShouldClearPenalty_WhenHasActivePenaltyIsFalse()
    {
        var target = Participant(Guid.NewGuid(), "Target");
        var game = Seed(null, "InProgress", null, target);
        var sut = StartedWith(game);
        // First give it a penalty via prey-updated.
        var ends = DateTimeOffset.Parse("2026-07-15T10:00:00Z");
        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.PreyUpdated,
            new PreyUpdatedPayload(target.UserId, "penalized", null, ends, "left-playfield"), seq: 1));
        Assert.Equal(ends, sut.CurrentState!.Participants.Single(p => p.UserId == target.UserId).PenaltyEndsAt);

        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.ParticipantChanged,
            new ParticipantPayload(target.UserId, "Target", IsReady: true, State: "Active", LastKnownLocation: null, HasActivePenalty: false),
            seq: 2));

        Assert.Null(sut.CurrentState!.Participants.Single(p => p.UserId == target.UserId).PenaltyEndsAt);
    }

    [Fact]
    public void ParticipantChanged_ShouldPreserveKnownPenaltyEndsAt_WhenHasActivePenaltyIsTrue()
    {
        var target = Participant(Guid.NewGuid(), "Target");
        var game = Seed(null, "InProgress", null, target);
        var sut = StartedWith(game);
        var ends = DateTimeOffset.Parse("2026-07-15T10:00:00Z");
        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.PreyUpdated,
            new PreyUpdatedPayload(target.UserId, "penalized", null, ends, "left-playfield"), seq: 1));

        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.ParticipantChanged,
            new ParticipantPayload(target.UserId, "Target", IsReady: true, State: "Active", LastKnownLocation: null, HasActivePenalty: true),
            seq: 2));

        Assert.Equal(ends, sut.CurrentState!.Participants.Single(p => p.UserId == target.UserId).PenaltyEndsAt);
    }

    [Fact]
    public void ParticipantRemoved_ShouldRemoveFromStateAndGame()
    {
        var target = Participant(Guid.NewGuid(), "Target");
        var other = Participant(Guid.NewGuid(), "Other");
        var game = Seed(null, "Lobby", null, target, other);
        var sut = StartedWith(game);

        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.ParticipantRemoved,
            new ParticipantRemovedPayload(target.UserId), seq: 1));

        Assert.Single(sut.CurrentState!.Participants);
        Assert.Single(sut.CurrentGame!.Participants);
        Assert.DoesNotContain(sut.CurrentState.Participants, p => p.UserId == target.UserId);
    }

    [Fact]
    public void ParticipantRemoved_UnknownUserId_ShouldBeNoOp()
    {
        var game = Seed(null, "Lobby", null, Participant(Guid.NewGuid(), "Known"));
        var sut = StartedWith(game);
        var before = sut.CurrentState;
        var broadcasts = 0;
        sut.Subscribe(_ => broadcasts++);

        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.ParticipantRemoved,
            new ParticipantRemovedPayload(Guid.NewGuid()), seq: 1));

        Assert.Same(before, sut.CurrentState);
        Assert.Equal(0, broadcasts);
    }

    // ---- configuration-changed ----

    [Fact]
    public void ConfigurationChanged_ShouldUpdateStatusAndConfig_AndPreserveParticipants()
    {
        var participant = Participant(Guid.NewGuid(), "Bob");
        var game = Seed(null, "Lobby", null, participant);
        var sut = StartedWith(game);

        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.ConfigurationChanged,
            new ConfigurationChangedPayload(
                game.Id, "9999", game.OwnerUserId, "InProgress",
                new GameConfigurationDetails(60, 10, 15, 180, 90),
                HunterUserId: null, Outcome: "None", CompletedAt: null),
            seq: 1));

        Assert.Equal("InProgress", sut.CurrentState!.Status);
        Assert.Equal("InProgress", sut.CurrentGame!.Status);
        Assert.Equal("9999", sut.CurrentGame.GameCode);
        Assert.Equal(60, sut.CurrentGame.Configuration.GameDuration);
        Assert.Single(sut.CurrentState.Participants);
        Assert.Equal(participant.UserId, sut.CurrentState.Participants[0].UserId);
        Assert.Single(sut.CurrentGame.Participants);
    }

    [Fact]
    public void ConfigurationChanged_ShouldDeriveHunter_AndRecomputePreysLeft()
    {
        var hunterId = Guid.NewGuid();
        var preyA = Participant(Guid.NewGuid(), "PreyA");
        var preyB = Participant(Guid.NewGuid(), "PreyB");
        var hunterParticipant = Participant(hunterId, "Hunter");
        var game = Seed(null, "Started", null, hunterParticipant, preyA, preyB);
        var sut = StartedWith(game);
        Assert.Equal(3, sut.CurrentState!.PreysLeft); // hunter not yet designated in the composite

        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.ConfigurationChanged,
            new ConfigurationChangedPayload(
                game.Id, game.GameCode, game.OwnerUserId, "InProgress", null,
                HunterUserId: hunterId, Outcome: "None", CompletedAt: null),
            seq: 1));

        Assert.Equal(hunterId, sut.CurrentState!.HunterUserId);
        Assert.Equal(2, sut.CurrentState.PreysLeft);
    }

    // ---- locations-updated (batched) ----

    [Fact]
    public void LocationsUpdated_ShouldUpdateOnlyNamedParticipants_Batched()
    {
        var a = Participant(Guid.NewGuid(), "A");
        var b = Participant(Guid.NewGuid(), "B");
        var c = Participant(Guid.NewGuid(), "C");
        var game = Seed(null, "InProgress", null, a, b, c);
        var sut = StartedWith(game);

        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.LocationsUpdated,
            new LocationsUpdatedPayload(new[]
            {
                new LocationEntry(a.UserId, "Prey", 51.1, 4.1, "Active"),
                new LocationEntry(b.UserId, "Prey", 51.2, 4.2, "Active"),
            }),
            seq: 1));

        var updatedA = sut.CurrentState!.Participants.Single(p => p.UserId == a.UserId);
        var updatedB = sut.CurrentState.Participants.Single(p => p.UserId == b.UserId);
        var untouchedC = sut.CurrentState.Participants.Single(p => p.UserId == c.UserId);
        Assert.Equal(51.1, updatedA.Location!.Latitude);
        Assert.Equal(51.2, updatedB.Location!.Latitude);
        Assert.Null(untouchedC.Location);

        var gameA = sut.CurrentGame!.Participants.Single(p => p.UserId == a.UserId);
        Assert.Equal(51.1, gameA.Latitude);
    }

    [Fact]
    public void LocationsUpdated_AllUnknownParticipants_ShouldBeNoOp()
    {
        var game = Seed(null, "InProgress", null, Participant(Guid.NewGuid(), "Known"));
        var sut = StartedWith(game);
        var before = sut.CurrentState;
        var broadcasts = 0;
        sut.Subscribe(_ => broadcasts++);

        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.LocationsUpdated,
            new LocationsUpdatedPayload(new[] { new LocationEntry(Guid.NewGuid(), "Prey", 1, 2, "Active") }),
            seq: 1));

        Assert.Same(before, sut.CurrentState);
        Assert.Equal(0, broadcasts);
    }

    // ---- prey-updated ----

    [Fact]
    public void PreyUpdated_Tagged_ShouldSetState_AndRecomputePreysLeft()
    {
        var hunterId = Guid.NewGuid();
        var target = Participant(Guid.NewGuid(), "Target", state: "Active");
        var other = Participant(Guid.NewGuid(), "Other", state: "Active");
        var game = Seed(null, "InProgress", hunterId, Participant(hunterId, "Hunter"), target, other);
        var sut = StartedWith(game);
        Assert.Equal(2, sut.CurrentState!.PreysLeft);

        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.PreyUpdated,
            new PreyUpdatedPayload(target.UserId, "tagged", "Tagged", null, null), seq: 1));

        Assert.Equal("Tagged", sut.CurrentState!.Participants.Single(p => p.UserId == target.UserId).State);
        Assert.Equal("Tagged", sut.CurrentGame!.Participants.Single(p => p.UserId == target.UserId).State);
        Assert.Equal(1, sut.CurrentState.PreysLeft);
    }

    [Fact]
    public void PreyUpdated_Penalized_ShouldRecordPenaltyEndsAt()
    {
        var target = Participant(Guid.NewGuid(), "Target");
        var game = Seed(null, "InProgress", null, target);
        var sut = StartedWith(game);
        var ends = DateTimeOffset.Parse("2026-07-15T10:00:00Z");

        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.PreyUpdated,
            new PreyUpdatedPayload(target.UserId, "penalized", null, ends, "OutOfBounds"), seq: 1));

        Assert.Equal(ends, sut.CurrentState!.Participants.Single(p => p.UserId == target.UserId).PenaltyEndsAt);
        Assert.Equal(ends, sut.CurrentGame!.Participants.Single(p => p.UserId == target.UserId).PenaltyEndsAt);
    }

    [Fact]
    public void PreyUpdated_PenaltyCleared_ShouldClearPenalty()
    {
        var target = Participant(Guid.NewGuid(), "Target");
        var game = Seed(null, "InProgress", null, target);
        var sut = StartedWith(game);
        var ends = DateTimeOffset.Parse("2026-07-15T10:00:00Z");
        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.PreyUpdated,
            new PreyUpdatedPayload(target.UserId, "penalized", null, ends, "OutOfBounds"), seq: 1));

        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.PreyUpdated,
            new PreyUpdatedPayload(target.UserId, "penalty-cleared", null, null, null), seq: 2));

        Assert.Null(sut.CurrentState!.Participants.Single(p => p.UserId == target.UserId).PenaltyEndsAt);
    }

    [Fact]
    public void PreyUpdated_UnknownParticipant_ShouldBeNoOp()
    {
        var game = Seed(null, "InProgress", null, Participant(Guid.NewGuid(), "Known"));
        var sut = StartedWith(game);
        var before = sut.CurrentState;

        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.PreyUpdated,
            new PreyUpdatedPayload(Guid.NewGuid(), "tagged", "Tagged", null, null), seq: 1));

        Assert.Same(before, sut.CurrentState);
    }

    // ---- game-ended ----

    [Fact]
    public void GameEnded_ShouldMarkStateCompleted_AndSetOutcome()
    {
        var game = Seed(null, "InProgress");
        var sut = StartedWith(game);

        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.GameEnded,
            new GameEndedPayload("PreyEscaped", 2, DateTimeOffset.Parse("2026-07-15T12:00:00Z")), seq: 1));

        Assert.Equal("Completed", sut.CurrentState!.Status);
        Assert.True(sut.CurrentState.IsCompleted);
        Assert.Equal("PreyEscaped", sut.CurrentState.Outcome);
    }

    // ---- resync-requested ----

    [Fact]
    public void ResyncRequested_ShouldTriggerFullReconcile()
    {
        var game = Seed(null, "InProgress");
        var sut = StartedWith(game);
        var refreshed = game with { Status = "Completed" };
        SetupReconcile(refreshed);

        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.ResyncRequested,
            new ResyncRequestedPayload("server-hint"), seq: 99));

        Assert.Equal("Completed", sut.CurrentState!.Status);
        _gameApi.Verify(a => a.GetGameAsync(game.Id, "access", It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    // ---- protocol version ----

    [Fact]
    public void UnsupportedVersion_ShouldTriggerResync_AndNotApplyTheDelta()
    {
        var game = Seed(null, "Lobby");
        var sut = StartedWith(game);
        var refreshed = game with { Status = "Ready" };
        SetupReconcile(refreshed);

        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.ConfigurationChanged,
            new ConfigurationChangedPayload(game.Id, game.GameCode, game.OwnerUserId, "InProgress", null, null, "None", null),
            version: 2, seq: 1));

        // The delta itself (status → InProgress) was never applied; the resync's own status is what shows.
        Assert.Equal("Ready", sut.CurrentState!.Status);
        _gameApi.Verify(a => a.GetGameAsync(game.Id, "access", It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    // ---- sequence gap detection ----

    [Fact]
    public void SequentialSeq_ShouldApplyEachDelta()
    {
        var target = Participant(Guid.NewGuid(), "Target", state: "Active");
        var game = Seed(null, "InProgress", null, target);
        var sut = StartedWith(game);

        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.PreyUpdated,
            new PreyUpdatedPayload(target.UserId, "tagged", "Tagged", null, null), seq: 1));
        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.PreyUpdated,
            new PreyUpdatedPayload(target.UserId, "tagged", "Active", null, null), seq: 2));

        Assert.Equal("Active", sut.CurrentState!.Participants.Single(p => p.UserId == target.UserId).State);
    }

    [Fact]
    public void SeqGap_ShouldTriggerResync_AndNotApplyTheDelta()
    {
        var target = Participant(Guid.NewGuid(), "Target", state: "Active");
        var game = Seed(null, "InProgress", null, target);
        var sut = StartedWith(game);
        var refreshed = game with { Status = "Completed" };
        SetupReconcile(refreshed);

        // seq 1 applies normally, then seq 3 (skipping 2) is a gap.
        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.PreyUpdated,
            new PreyUpdatedPayload(target.UserId, "tagged", "Tagged", null, null), seq: 1));
        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.PreyUpdated,
            new PreyUpdatedPayload(target.UserId, "tagged", "Active", null, null), seq: 3));

        // The seq-3 delta was not applied — the resync's status is what shows, and the tag from seq 1 is
        // overwritten by the fresh snapshot (which still reports the original "Active" participant state).
        Assert.Equal("Completed", sut.CurrentState!.Status);
        _gameApi.Verify(a => a.GetGameAsync(game.Id, "access", It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Fact]
    public void SeqRegression_ShouldTriggerResync_AndNotApplyTheDelta()
    {
        var target = Participant(Guid.NewGuid(), "Target", state: "Active");
        var game = Seed(null, "InProgress", null, target);
        var sut = StartedWith(game);
        var refreshed = game with { Status = "Completed" };
        SetupReconcile(refreshed);

        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.PreyUpdated,
            new PreyUpdatedPayload(target.UserId, "tagged", "Tagged", null, null), seq: 5));
        // A regression (same or lower seq) after seq 5.
        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.PreyUpdated,
            new PreyUpdatedPayload(target.UserId, "tagged", "Active", null, null), seq: 5));

        Assert.Equal("Completed", sut.CurrentState!.Status);
        _gameApi.Verify(a => a.GetGameAsync(game.Id, "access", It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Fact]
    public void SeqTracker_ShouldReset_AfterReconcile_SoNextSeqBecomesNewBaseline()
    {
        var target = Participant(Guid.NewGuid(), "Target", state: "Active");
        var game = Seed(null, "InProgress", null, target);
        var sut = StartedWith(game); // reconciled once — tracker is reset (null)

        // The very first delta's seq (even a large jump like 50) is accepted as the new baseline.
        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.PreyUpdated,
            new PreyUpdatedPayload(target.UserId, "tagged", "Tagged", null, null), seq: 50));

        Assert.Equal("Tagged", sut.CurrentState!.Participants.Single(p => p.UserId == target.UserId).State);

        // And now seq 51 (continuous from 50) applies normally too.
        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.PreyUpdated,
            new PreyUpdatedPayload(target.UserId, "tagged", "Active", null, null), seq: 51));

        Assert.Equal("Active", sut.CurrentState!.Participants.Single(p => p.UserId == target.UserId).State);
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

        _connection.RaiseEnvelope(Envelope("totally-unknown", new { anything = 1 }, seq: 1));

        Assert.Same(before, sut.CurrentState);
        Assert.Equal(0, broadcasts);
    }

    [Fact]
    public void EnvelopeWithEmptyType_ShouldBeIgnored()
    {
        var game = Seed(null, "InProgress");
        var sut = StartedWith(game);
        var before = sut.CurrentState;

        _connection.RaiseEnvelope(Envelope("", new { }, seq: 1));

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

        // A delta whose data is a bare string → deserialization fails → no-op.
        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.PreyUpdated, "not-an-object", seq: 1));

        Assert.Same(before, sut.CurrentState);
        Assert.Equal(0, broadcasts);
    }

    [Fact]
    public void DeltaBeforeAnySnapshot_ShouldBeNoOp()
    {
        var sut = CreateSut();

        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.ParticipantRemoved,
            new ParticipantRemovedPayload(Guid.NewGuid()), seq: 1));

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

        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.GameEnded,
            new GameEndedPayload("PreyEscaped", 1, null), seq: 1));

        Assert.Equal("Completed", a!.State.Status);
        Assert.Equal("Completed", b!.State.Status);
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
            _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.GameEnded,
                new GameEndedPayload("PreyEscaped", 1, null), seq: 1)));

        Assert.Null(ex);
        Assert.True(secondCalled);
    }

    [Fact]
    public void UnsubscribedHandler_ShouldStopReceiving()
    {
        var target = Participant(Guid.NewGuid(), "Target");
        var game = Seed(null, "InProgress", null, target);
        var sut = StartedWith(game);
        var count = 0;
        void Handler(GameStateChanged _) => count++;
        sut.Subscribe(Handler);

        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.PreyUpdated,
            new PreyUpdatedPayload(target.UserId, "tagged", "Tagged", null, null), seq: 1));
        sut.Unsubscribe(Handler);
        _connection.RaiseEnvelope(Envelope(GameRealtimeEventTypes.PreyUpdated,
            new PreyUpdatedPayload(target.UserId, "tagged", "Active", null, null), seq: 2));

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
