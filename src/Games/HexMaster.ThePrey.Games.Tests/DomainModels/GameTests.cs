using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Tests.Factories;

namespace HexMaster.ThePrey.Games.Tests.DomainModels;

public sealed class GameTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ShouldStartInLobby_WithEmptyLobby()
    {
        var ownerId = Guid.NewGuid();
        var playfieldId = Guid.NewGuid();

        var game = Game.Create(ownerId, playfieldId, "0123", GameFaker.ValidConfiguration());

        Assert.NotEqual(Guid.Empty, game.Id);
        Assert.Equal("0123", game.GameCode);
        Assert.Equal(ownerId, game.OwnerUserId);
        Assert.Equal(playfieldId, game.PlayfieldId);
        Assert.Equal(GameStatus.Lobby, game.Status);
        Assert.Empty(game.Lobby);
        Assert.Null(game.Hunter);
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

        Assert.Single(game.Lobby);
    }

    [Fact]
    public void JoinLobby_ShouldThrow_WhenPlayerAlreadyInLobby()
    {
        var game = GameFaker.LobbyGame();
        var player = GameFaker.Player();
        game.JoinLobby(player);

        Assert.Throws<PlayerAlreadyInLobbyException>(() => game.JoinLobby(GameFaker.Player(player.UserId)));
        Assert.Single(game.Lobby);
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
        Assert.Equal(Game.MaxLobbySize, game.Lobby.Count);
    }

    [Fact]
    public void Start_ShouldDesignateHunterAndPreys()
    {
        var game = GameFaker.LobbyGameWithPlayers(3, out var ids);
        var hunterId = ids[0];

        game.Start(hunterId, Start);

        Assert.Equal(GameStatus.InProgress, game.Status);
        Assert.Equal(Start, game.StartedAt);
        Assert.NotNull(game.Hunter);
        Assert.Equal(hunterId, game.Hunter!.UserId);
        Assert.Equal(2, game.Preys.Count);
        Assert.DoesNotContain(game.Preys, p => p.UserId == hunterId);
    }

    [Fact]
    public void Start_ShouldThrow_WhenHunterNotInLobby()
    {
        var game = GameFaker.LobbyGameWithPlayers(2, out _);

        Assert.Throws<InvalidOperationException>(() => game.Start(Guid.NewGuid(), Start));
        Assert.Equal(GameStatus.Lobby, game.Status);
    }

    [Fact]
    public void Start_ShouldThrow_WhenFewerThanTwoPlayers()
    {
        var game = GameFaker.LobbyGameWithPlayers(1, out var ids);

        Assert.Throws<InvalidOperationException>(() => game.Start(ids[0], Start));
        Assert.Equal(GameStatus.Lobby, game.Status);
    }

    [Fact]
    public void Start_ShouldThrow_WhenAlreadyStarted()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, Start);

        Assert.Throws<InvalidOperationException>(() => game.Start(hunterId, Start));
    }

    [Fact]
    public void Start_ShouldThrow_WhenANonOwnerPlayerIsNotReady()
    {
        var game = GameFaker.LobbyGameWithPlayers(3, out var ids, markReady: false);
        // Only two of the three players ready up; the third blocks the start.
        game.SetReady(ids[0]);
        game.SetReady(ids[1]);

        Assert.Throws<InvalidOperationException>(() => game.Start(ids[0], Start));
        Assert.Equal(GameStatus.Lobby, game.Status);
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

        var prey = game.Preys.Single(p => p.UserId == preyId);
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

        var prey = game.Preys.Single(p => p.UserId == preyIds[0]);
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
}
