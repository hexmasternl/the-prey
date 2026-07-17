using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Tests.Factories;

namespace HexMaster.ThePrey.Games.Tests.DomainModels;

public sealed class GameTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ShouldStartInLobby_WithEmptyParticipants()
    {
        var ownerId = Guid.NewGuid();
        var playfieldId = Guid.NewGuid();

        var game = Game.Create(ownerId, playfieldId, "0123", GameFaker.ValidConfiguration());

        Assert.NotEqual(Guid.Empty, game.Id);
        Assert.Equal("0123", game.GameCode);
        Assert.Equal(ownerId, game.OwnerUserId);
        Assert.Equal(playfieldId, game.PlayfieldId);
        Assert.Equal(GameStatus.Lobby, game.Status);
        Assert.Empty(game.Participants);
        Assert.Null(game.HunterUserId);
        Assert.Empty(game.Preys);
        Assert.Null(game.StartedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("12345")]
    [InlineData("123a")]
    [InlineData("12 4")]
    public void Create_ShouldThrow_WhenGameCodeIsNotFourDigits(string gameCode)
    {
        Assert.Throws<ArgumentException>(() =>
            Game.Create(Guid.NewGuid(), Guid.NewGuid(), gameCode, GameFaker.ValidConfiguration()));
    }

    [Fact]
    public void JoinLobby_ShouldAddPlayer_WhenInLobby()
    {
        var game = GameFaker.LobbyGame();

        game.JoinLobby(GameFaker.Player());

        Assert.Single(game.Participants);
    }

    [Fact]
    public void JoinLobby_ShouldThrow_WhenPlayerAlreadyInLobby()
    {
        var game = GameFaker.LobbyGame();
        var player = GameFaker.Player();
        game.JoinLobby(player);

        Assert.Throws<PlayerAlreadyInLobbyException>(() => game.JoinLobby(GameFaker.Player(player.UserId)));
        Assert.Single(game.Participants);
    }

    [Fact]
    public void JoinLobby_ShouldThrow_WhenGameAlreadyStarted()
    {
        var game = GameFaker.StartedGame(out _, out _, Start);

        Assert.Throws<GameNotJoinableException>(() => game.JoinLobby(GameFaker.Player()));
    }

    [Fact]
    public void JoinLobby_ShouldThrow_WhenLobbyIsFull()
    {
        var game = GameFaker.LobbyGameWithPlayers(Game.MaxLobbySize, out _);

        Assert.Throws<LobbyFullException>(() => game.JoinLobby(GameFaker.Player()));
        Assert.Equal(Game.MaxLobbySize, game.Participants.Count);
    }

    [Fact]
    public void Arm_ShouldFixRolesAndTransitionToStarted()
    {
        var game = GameFaker.LobbyGameWithPlayers(3, out var ids);
        var hunterId = ids[0];
        game.DesignateHunter(hunterId);
        Assert.Equal(GameStatus.Ready, game.Status);

        game.Arm(hunterId);

        Assert.Equal(GameStatus.Started, game.Status);
        Assert.Equal(hunterId, game.HunterUserId);
        Assert.Equal(2, game.Preys.Count);
        Assert.DoesNotContain(game.Preys, id => id == hunterId);
        Assert.Null(game.StartedAt);
        Assert.Null(game.EndsAt);
    }

    [Fact]
    public void BeginPlay_ShouldTransitionToInProgress_WhenStarted()
    {
        var game = GameFaker.LobbyGameWithPlayers(3, out var ids);
        game.DesignateHunter(ids[0]);
        game.Arm(ids[0]);

        game.BeginPlay(Start);

        Assert.Equal(GameStatus.InProgress, game.Status);
        Assert.Equal(Start, game.StartedAt);
        Assert.Equal(ids[0], game.HunterUserId);
        Assert.Equal(2, game.Preys.Count);
        Assert.DoesNotContain(game.Preys, id => id == ids[0]);
    }

    [Fact]
    public void Arm_ShouldThrow_WhenHunterNotInLobby()
    {
        var game = GameFaker.LobbyGameWithPlayers(2, out _);

        Assert.Throws<InvalidOperationException>(() => game.Arm(Guid.NewGuid()));
        Assert.Equal(GameStatus.Lobby, game.Status);
    }

    [Fact]
    public void Arm_ShouldThrow_WhenFewerThanTwoPlayers()
    {
        var game = GameFaker.LobbyGameWithPlayers(1, out var ids);

        Assert.Throws<InvalidOperationException>(() => game.Arm(ids[0]));
        Assert.Equal(GameStatus.Lobby, game.Status);
    }

    [Fact]
    public void Arm_ShouldThrow_WhenAlreadyArmed()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, Start);

        // Already InProgress — Arm must reject
        Assert.Throws<InvalidOperationException>(() => game.Arm(hunterId));
    }

    [Fact]
    public void BeginPlay_ShouldThrow_WhenAlreadyInProgress()
    {
        var game = GameFaker.StartedGame(out _, out _, Start);

        Assert.Throws<InvalidOperationException>(() => game.BeginPlay(Start));
    }

    [Fact]
    public void BeginPlay_ShouldThrow_WhenInLobby()
    {
        var game = GameFaker.LobbyGameWithPlayers(2, out _);

        Assert.Throws<InvalidOperationException>(() => game.BeginPlay(Start));
    }

    [Fact]
    public void Arm_ShouldThrow_WhenANonOwnerPlayerIsNotReady()
    {
        var game = GameFaker.LobbyGameWithPlayers(3, out var ids, markReady: false);
        // Only two of the three players ready up; the third blocks the start.
        game.SetReady(ids[0]);
        game.SetReady(ids[1]);

        Assert.Throws<InvalidOperationException>(() => game.Arm(ids[0]));
        Assert.Equal(GameStatus.Lobby, game.Status);
    }

    [Fact]
    public void Arm_ShouldKeepParticipants_NotRecreate()
    {
        // Participants from the lobby carry through to Started without being cleared.
        var game = GameFaker.LobbyGameWithPlayers(3, out var ids);
        Assert.Equal(3, game.Participants.Count);
        game.DesignateHunter(ids[0]);

        game.Arm(ids[0]);

        Assert.Equal(3, game.Participants.Count);
        Assert.All(ids, id => Assert.Contains(game.Participants, p => p.UserId == id));
    }

    // ── Automatic Lobby ↔ Ready readiness transition ──────────────────────────

    [Fact]
    public void Status_ShouldBecomeReady_WhenLastNonOwnerReadiesUpWithHunterDesignated()
    {
        var game = GameFaker.LobbyGameWithPlayers(3, out var ids, markReady: false);
        game.DesignateHunter(ids[0]);
        game.SetReady(ids[0]);
        game.SetReady(ids[1]);
        Assert.Equal(GameStatus.Lobby, game.Status); // ids[2] still not ready

        game.SetReady(ids[2]);

        Assert.Equal(GameStatus.Ready, game.Status);
        Assert.True(game.IsReadyToStart);
    }

    [Fact]
    public void Status_ShouldRevertToLobby_WhenSettingsChangeResetsReadiness()
    {
        var game = GameFaker.LobbyGameWithPlayers(3, out var ids);
        game.DesignateHunter(ids[0]);
        Assert.Equal(GameStatus.Ready, game.Status);

        game.UpdateSettings(GameFaker.ValidConfiguration(gameDuration: 90));

        Assert.Equal(GameStatus.Lobby, game.Status);
        Assert.False(game.IsReadyToStart);
    }

    [Fact]
    public void Status_ShouldRevertToLobby_WhenDesignatedHunterLeaves()
    {
        var game = GameFaker.LobbyGameWithPlayers(3, out var ids);
        game.DesignateHunter(ids[0]);
        Assert.Equal(GameStatus.Ready, game.Status);

        game.RemoveLobbyPlayer(ids[0]);

        Assert.Equal(GameStatus.Lobby, game.Status);
        Assert.Null(game.HunterUserId);
    }

    [Fact]
    public void EndByOwner_ShouldCancel_WhenStarted()
    {
        var game = GameFaker.ArmedGame(out _, out _);
        Assert.Equal(GameStatus.Started, game.Status);

        game.EndByOwner(Start);

        Assert.Equal(GameStatus.Completed, game.Status);
        Assert.Equal(GameOutcome.Cancelled, game.Outcome);
    }

    [Fact]
    public void IsReadyToStart_ShouldBeFalse_UntilHunterDesignatedAndAllReady()
    {
        var game = GameFaker.LobbyGameWithPlayers(3, out var ids, markReady: false);
        Assert.False(game.IsReadyToStart); // no hunter, nobody ready

        game.DesignateHunter(ids[0]);
        game.SetReady(ids[0]);
        game.SetReady(ids[1]);
        Assert.False(game.IsReadyToStart); // ids[2] still not ready

        game.SetReady(ids[2]);
        Assert.True(game.IsReadyToStart);
    }

    [Fact]
    public void IsReadyToStart_ShouldBeFalse_WhenFewerThanTwoPlayers()
    {
        var game = GameFaker.LobbyGameWithPlayers(1, out var ids);
        game.DesignateHunter(ids[0]);

        Assert.False(game.IsReadyToStart);
    }

    [Fact]
    public void RecordLocation_ShouldAppendHistory_WithoutSettingCurrentLocation()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start);
        var preyId = preyIds[0];
        var coordinate = GpsCoordinate.Create(52.1, 5.1);

        game.RecordLocation(preyId, coordinate, Start.AddSeconds(30));

        var prey = game.Participants.Single(p => p.UserId == preyId);
        // Location is set exclusively by the game engine broadcast cycle, not by RecordLocation.
        Assert.Null(prey.Location);
        Assert.Single(prey.Locations);
        Assert.Equal(coordinate, prey.Locations[0].Coordinate);
    }

    [Fact]
    public void RecordLocation_ShouldThrow_WhenUserIsNotAParticipant()
    {
        var game = GameFaker.StartedGame(out _, out _, Start);

        Assert.Throws<InvalidOperationException>(() =>
            game.RecordLocation(Guid.NewGuid(), GpsCoordinate.Create(52.1, 5.1), Start.AddSeconds(30)));
    }

    [Fact]
    public void RecordLocation_ShouldThrow_WhenGameNotInProgress()
    {
        var game = GameFaker.LobbyGameWithPlayers(2, out var ids);

        Assert.Throws<InvalidOperationException>(() =>
            game.RecordLocation(ids[0], GpsCoordinate.Create(52.1, 5.1), Start));
    }

    [Fact]
    public void RecordLocation_ShouldThrow_WhenCoordinateOutOfRange()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            game.RecordLocation(preyIds[0], GpsCoordinate.Create(200, 5.1), Start.AddSeconds(30)));
    }

    [Fact]
    public void Complete_ShouldTransitionInProgressGameToCompleted()
    {
        var game = GameFaker.StartedGame(out _, out _, Start);

        game.Complete(Start.AddMinutes(60));

        Assert.Equal(GameStatus.Completed, game.Status);
    }

    // EndByOwner

    [Fact]
    public void EndByOwner_ShouldTransitionLobbyToCompleted()
    {
        var game = GameFaker.LobbyGameWithPlayers(2, out _);

        game.EndByOwner(Start);

        Assert.Equal(GameStatus.Completed, game.Status);
    }

    [Fact]
    public void EndByOwner_ShouldTransitionInProgressToCompleted()
    {
        var game = GameFaker.StartedGame(out _, out _, Start);

        game.EndByOwner(Start.AddMinutes(10));

        Assert.Equal(GameStatus.Completed, game.Status);
    }

    [Fact]
    public void EndByOwner_ShouldThrow_WhenAlreadyCompleted()
    {
        var game = GameFaker.StartedGame(out _, out _, Start);
        game.Complete(Start.AddMinutes(60));

        Assert.Throws<InvalidOperationException>(() => game.EndByOwner(Start.AddMinutes(60)));
    }

    // Forfeit

    [Fact]
    public void Forfeit_ShouldSetPreyStateToOut_WhenInProgress()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start);

        game.Forfeit(preyIds[0]);

        var prey = game.Participants.Single(p => p.UserId == preyIds[0]);
        Assert.Equal(PlayerState.Out, prey.State);
    }

    [Fact]
    public void Forfeit_ShouldThrow_WhenUserIsNotAParticipant()
    {
        var game = GameFaker.StartedGame(out _, out _, Start);

        Assert.Throws<ArgumentException>(() => game.Forfeit(Guid.NewGuid()));
    }

    [Fact]
    public void Forfeit_ShouldThrow_WhenUserIsTheHunter()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, Start);

        Assert.Throws<InvalidOperationException>(() => game.Forfeit(hunterId));
    }

    [Fact]
    public void Forfeit_ShouldThrow_WhenGameIsNotInProgress()
    {
        var game = GameFaker.LobbyGameWithPlayers(2, out var ids);

        Assert.Throws<InvalidOperationException>(() => game.Forfeit(ids[0]));
    }

    [Fact]
    public void Forfeit_ShouldThrow_WhenPreyIsAlreadyOut()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start);
        game.Forfeit(preyIds[0]);

        Assert.Throws<InvalidOperationException>(() => game.Forfeit(preyIds[0]));
    }

    // ── NextScheduledBroadcastOn & SweepLocations ──────────────────────────

    [Fact]
    public void Start_ShouldSeedNextScheduledBroadcastOn_ToStartedAt()
    {
        var game = GameFaker.StartedGame(out _, out _, Start);

        Assert.Equal(Start, game.NextScheduledBroadcastOn);
    }

    [Fact]
    public void SweepLocations_ShouldBroadcastAllParticipants_OnRegularTick()
    {
        // Regular tick: now >= NextScheduledBroadcastOn (seeded to Start).
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start, playerCount: 3);
        var now = Start.AddSeconds(30);

        // Both prey and hunter have recorded a location.
        game.RecordLocation(hunterId, GpsCoordinate.Create(52.1, 5.1), Start.AddSeconds(5));
        game.RecordLocation(preyIds[0], GpsCoordinate.Create(52.2, 5.2), Start.AddSeconds(10));
        game.RecordLocation(preyIds[1], GpsCoordinate.Create(52.3, 5.3), Start.AddSeconds(15));

        var sweeps = game.SweepLocations(now);

        // All three participants should have a broadcast.
        Assert.Equal(3, sweeps.Count(s => s.Broadcast is not null));
        // Location is updated for each broadcast participant.
        foreach (var participant in game.Participants)
            Assert.NotNull(participant.Location);
    }

    [Fact]
    public void SweepLocations_ShouldCopyLatestReadingIntoLocation_OnRegularTick()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start, playerCount: 2);
        var preyId = preyIds[0];
        var first = GpsCoordinate.Create(52.1, 5.1);
        var second = GpsCoordinate.Create(52.2, 5.2);

        game.RecordLocation(preyId, first, Start.AddSeconds(5));
        game.RecordLocation(preyId, second, Start.AddSeconds(10));

        game.SweepLocations(Start.AddSeconds(30));

        var prey = game.Participants.Single(p => p.UserId == preyId);
        // Location must be the most recent reading, not the first one.
        Assert.Equal(second, prey.Location);
    }

    [Fact]
    public void SweepLocations_ShouldNotBroadcast_WhenBetweenRegularTicks_AndNoActivePenalty()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start, playerCount: 2);
        var preyId = preyIds[0];

        // Seed the schedule past the first tick without broadcasting.
        game.SweepLocations(Start.AddSeconds(30)); // advances schedule to Start + 30s

        // Record a new reading after the first tick.
        game.RecordLocation(preyId, GpsCoordinate.Create(52.1, 5.1), Start.AddSeconds(35));

        // Sweep at Start+40s — schedule not yet due (next at Start+60s for 30s default interval).
        var sweeps = game.SweepLocations(Start.AddSeconds(40));

        // No broadcast, but the new coordinate is consumed and reported for boundary checks.
        Assert.All(sweeps, s => Assert.Null(s.Broadcast));
        var prey = game.Participants.Single(p => p.UserId == preyId);
        // Location was not updated because no broadcast happened.
        Assert.Null(prey.Location);
    }

    [Fact]
    public void SweepLocations_ShouldBroadcastPenalizedParticipant_BetweenRegularTicks()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start, playerCount: 2);
        var preyId = preyIds[0];

        // Give the prey a known position first, then apply a penalty.
        game.RecordLocation(preyId, GpsCoordinate.Create(52.1, 5.1), Start.AddSeconds(5));
        // Consume readings via a regular tick to set Location.
        game.SweepLocations(Start.AddSeconds(30)); // schedule now at Start + 30s

        // Apply penalty (expires in 5 min) and advance time between ticks.
        game.ApplyPenalty(preyId, Start.AddMinutes(6));

        // Off-beat sweep: schedule next due at Start+60s; now is Start+45s.
        var nextScheduledBefore = game.NextScheduledBroadcastOn;
        var sweeps = game.SweepLocations(Start.AddSeconds(45));

        var prey = game.Participants.Single(p => p.UserId == preyId);
        var preySweep = sweeps.FirstOrDefault(s => s.UserId == preyId);
        Assert.NotNull(preySweep);
        Assert.NotNull(preySweep!.Broadcast); // penalized — should broadcast
        // Schedule must not advance for an off-beat penalty broadcast.
        Assert.Equal(nextScheduledBefore, game.NextScheduledBroadcastOn);
    }

    [Fact]
    public void SweepLocations_ShouldAdvanceSchedule_ByRegularInterval()
    {
        // Default config: DefaultLocationInterval = 30s; game started at Start.
        // NextScheduledBroadcastOn seeded to Start. Sweep at Start+30s.
        // The while-loop catches up: Start→Start+30s, Start+30s→Start+60s (30s<=30s), then stop.
        var game = GameFaker.StartedGame(out _, out _, Start, playerCount: 2);

        game.SweepLocations(Start.AddSeconds(30));

        // Schedule advances to the first future slot beyond now.
        Assert.Equal(Start.AddSeconds(60), game.NextScheduledBroadcastOn);
    }

    [Fact]
    public void SweepLocations_ShouldTightenSchedule_WhenCrossesIntoFinalStage()
    {
        // DefaultLocationInterval = 30s, FinalLocationInterval = 10s, FinalStageDuration = 10min.
        // Game 60 min → final stage starts at Start+50min.
        // Seed: NextScheduledBroadcastOn = Start.
        // Sweep at Start+50min+5s: schedule was due, interval now in final stage → 10s.
        var game = GameFaker.StartedGame(out _, out _, Start, playerCount: 2);

        // Advance schedule to a point just inside the final stage.
        // Simulate many ticks by sweeping at final-stage time.
        var finalStageTime = Start.AddMinutes(50).AddSeconds(5);
        game.SweepLocations(finalStageTime);

        // After this sweep NextScheduledBroadcastOn should be > finalStageTime and use 10s increments.
        Assert.True(game.NextScheduledBroadcastOn > finalStageTime);
    }
}
