using HexMaster.ThePrey.Games.DomainModels;

namespace HexMaster.ThePrey.GameEngine.Tests;

/// <summary>Tests for the location broadcast selection logic.</summary>
public sealed class LocationBroadcastTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 6, 5, 10, 0, 0, TimeSpan.Zero);

    private static (Game game, Guid hunterId, Guid preyId) CreateTwoPlayerGame()
    {
        var config = GameConfiguration.Create(60, 5, 10, 30, 10);
        var game = Game.Create(Guid.NewGuid(), Guid.NewGuid(), "1234", config);
        var hunterId = Guid.NewGuid();
        var preyId = Guid.NewGuid();
        game.JoinLobby(GameParticipant.Create(hunterId, "Hunter", null));
        game.JoinLobby(GameParticipant.Create(preyId, "Prey", null));
        game.SetReady(hunterId);
        game.SetReady(preyId);
        game.Start(hunterId, StartedAt);
        return (game, hunterId, preyId);
    }

    [Fact]
    public void Participant_WithNoLocationHistory_ShouldNotBeIncludedInUpdates()
    {
        var (game, hunterId, _) = CreateTwoPlayerGame();

        // Hunter has no location history — simulate what engine would do
        var hunter = game.Participants.Single(p => p.UserId == hunterId);
        Assert.Empty(hunter.Locations);

        var mostRecent = hunter.Locations
            .OrderByDescending(l => l.RecordedAt)
            .FirstOrDefault();

        Assert.Null(mostRecent);
    }

    [Fact]
    public void Participant_WithLocationHistory_ShouldHaveMostRecentSelected()
    {
        var (game, hunterId, _) = CreateTwoPlayerGame();

        var earlier = StartedAt.AddSeconds(20);
        var later = StartedAt.AddSeconds(40);

        game.RecordLocation(hunterId, new GpsCoordinate(52.0, 4.0), earlier);
        game.RecordLocation(hunterId, new GpsCoordinate(52.1, 4.1), later);

        var hunter = game.Participants.Single(p => p.UserId == hunterId);
        var mostRecent = hunter.Locations
            .OrderByDescending(l => l.RecordedAt)
            .First();

        Assert.Equal(later, mostRecent.RecordedAt);
        Assert.Equal(52.1, mostRecent.Coordinate.Latitude);
        Assert.Equal(4.1, mostRecent.Coordinate.Longitude);
    }

    [Fact]
    public void UpdateBroadcastLocation_ShouldSetLocationProperty()
    {
        var (game, hunterId, _) = CreateTwoPlayerGame();

        var hunter = game.Participants.Single(p => p.UserId == hunterId);
        Assert.Null(hunter.Location);

        var coord = new GpsCoordinate(52.5, 5.0);
        hunter.UpdateBroadcastLocation(coord);

        Assert.NotNull(hunter.Location);
        Assert.Equal(52.5, hunter.Location.Latitude);
        Assert.Equal(5.0, hunter.Location.Longitude);
    }

    [Fact]
    public void RecordLocation_ShouldNotUpdateLocation_AfterTask92Fix()
    {
        // Task 9.2: RecordLocation must NOT set Location — that is exclusively engine's job
        var (game, hunterId, _) = CreateTwoPlayerGame();

        game.RecordLocation(hunterId, new GpsCoordinate(52.0, 4.0), StartedAt.AddSeconds(30));

        var hunter = game.Participants.Single(p => p.UserId == hunterId);
        // Location should remain null after RecordLocation since we removed the assignment
        Assert.Null(hunter.Location);
        Assert.Single(hunter.Locations);
    }
}
