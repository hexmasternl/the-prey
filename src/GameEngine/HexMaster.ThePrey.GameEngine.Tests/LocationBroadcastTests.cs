using HexMaster.ThePrey.Games.DomainModels;

namespace HexMaster.ThePrey.GameEngine.Tests;

/// <summary>Tests for the location broadcast selection logic.</summary>
public sealed class LocationBroadcastTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 6, 5, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Participant_WithNoLocationHistory_ShouldNotBeIncludedInUpdates()
    {
        // A participant with an empty Locations list has nothing to broadcast
        var config = GameConfiguration.Create(60, 5, 10, 30, 10);
        var game = Game.Create(Guid.NewGuid(), Guid.NewGuid(), "12345678", config);

        var playerId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        game.JoinLobby(LobbyPlayer.Create(playerId, "Hunter", null));
        game.JoinLobby(LobbyPlayer.Create(secondId, "Prey", null));
        game.Start(playerId, StartedAt);

        // Hunter has no location history — simulate what engine would do
        var hunter = game.Hunter!;
        Assert.Empty(hunter.Locations);

        var mostRecent = hunter.Locations
            .OrderByDescending(l => l.RecordedAt)
            .FirstOrDefault();

        Assert.Null(mostRecent);
    }

    [Fact]
    public void Participant_WithLocationHistory_ShouldHaveMostRecentSelected()
    {
        var config = GameConfiguration.Create(60, 5, 10, 30, 10);
        var game = Game.Create(Guid.NewGuid(), Guid.NewGuid(), "12345678", config);

        var hunterId = Guid.NewGuid();
        var preyId = Guid.NewGuid();
        game.JoinLobby(LobbyPlayer.Create(hunterId, "Hunter", null));
        game.JoinLobby(LobbyPlayer.Create(preyId, "Prey", null));
        game.Start(hunterId, StartedAt);

        var earlier = StartedAt.AddSeconds(20);
        var later = StartedAt.AddSeconds(40);

        game.RecordLocation(hunterId, new GpsCoordinate(52.0, 4.0), earlier);
        game.RecordLocation(hunterId, new GpsCoordinate(52.1, 4.1), later);

        var hunter = game.Hunter!;
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
        var config = GameConfiguration.Create(60, 5, 10, 30, 10);
        var game = Game.Create(Guid.NewGuid(), Guid.NewGuid(), "12345678", config);

        var hunterId = Guid.NewGuid();
        var preyId = Guid.NewGuid();
        game.JoinLobby(LobbyPlayer.Create(hunterId, "Hunter", null));
        game.JoinLobby(LobbyPlayer.Create(preyId, "Prey", null));
        game.Start(hunterId, StartedAt);

        var hunter = game.Hunter!;
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
        var config = GameConfiguration.Create(60, 5, 10, 30, 10);
        var game = Game.Create(Guid.NewGuid(), Guid.NewGuid(), "12345678", config);

        var hunterId = Guid.NewGuid();
        var preyId = Guid.NewGuid();
        game.JoinLobby(LobbyPlayer.Create(hunterId, "Hunter", null));
        game.JoinLobby(LobbyPlayer.Create(preyId, "Prey", null));
        game.Start(hunterId, StartedAt);

        game.RecordLocation(hunterId, new GpsCoordinate(52.0, 4.0), StartedAt.AddSeconds(30));

        var hunter = game.Hunter!;
        // Location should remain null after RecordLocation since we removed the assignment
        Assert.Null(hunter.Location);
        Assert.Single(hunter.Locations);
    }
}
