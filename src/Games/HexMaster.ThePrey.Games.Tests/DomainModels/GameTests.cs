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

        var game = Game.Create(ownerId, playfieldId, GameFaker.ValidConfiguration());

        Assert.NotEqual(Guid.Empty, game.Id);
        Assert.Equal(ownerId, game.OwnerUserId);
        Assert.Equal(playfieldId, game.PlayfieldId);
        Assert.Equal(GameStatus.Lobby, game.Status);
        Assert.Empty(game.Lobby);
        Assert.Null(game.Hunter);
        Assert.Empty(game.Preys);
        Assert.Null(game.StartedAt);
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

        Assert.Throws<InvalidOperationException>(() => game.JoinLobby(GameFaker.Player(player.UserId)));
        Assert.Single(game.Lobby);
    }

    [Fact]
    public void JoinLobby_ShouldThrow_WhenGameAlreadyStarted()
    {
        var game = GameFaker.StartedGame(out _, out _, Start);

        Assert.Throws<InvalidOperationException>(() => game.JoinLobby(GameFaker.Player()));
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
    public void RecordLocation_ShouldAppendHistoryAndUpdateCurrentLocation()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start);
        var preyId = preyIds[0];
        var coordinate = GpsCoordinate.Create(52.1, 5.1);

        game.RecordLocation(preyId, coordinate, Start.AddSeconds(30));

        var prey = game.Preys.Single(p => p.UserId == preyId);
        Assert.Equal(coordinate, prey.Location);
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
}
