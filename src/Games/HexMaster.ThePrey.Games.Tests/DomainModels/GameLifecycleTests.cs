using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Tests.Factories;

namespace HexMaster.ThePrey.Games.Tests.DomainModels;

public sealed class GameLifecycleTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 9, 10, 0, 0, TimeSpan.Zero);

    // ── Task 5.1: CreatedAt and CleanUpAfter set on Create ────────────────────

    [Fact]
    public void Create_ShouldSetCreatedAt_ToApproximatelyNow()
    {
        var before = DateTimeOffset.UtcNow;
        var game = GameFaker.LobbyGame();
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(game.CreatedAt, before, after);
    }

    [Fact]
    public void Create_ShouldSetCleanUpAfter_ToCreatedAtPlusCleanupWindow()
    {
        var game = GameFaker.LobbyGame();

        var expected = game.CreatedAt.AddHours(Game.CleanupWindowHours);

        Assert.Equal(expected, game.CleanUpAfter);
    }

    [Fact]
    public void Create_ShouldSetCleanUpAfter_To48HoursAfterCreation()
    {
        var before = DateTimeOffset.UtcNow;
        var game = GameFaker.LobbyGame();
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(game.CleanUpAfter, before.AddHours(48), after.AddHours(48));
    }

    // ── Task 5.2: EndsAt set on BeginPlay ────────────────────────────────────

    [Fact]
    public void Create_ShouldLeaveEndsAt_Null()
    {
        var game = GameFaker.LobbyGame();

        Assert.Null(game.EndsAt);
    }

    [Fact]
    public void Arm_ShouldLeaveEndsAt_Null()
    {
        var config = GameFaker.ValidConfiguration(gameDuration: 60);
        var game = GameFaker.LobbyGameWithPlayers(2, out var ids, config);
        game.DesignateHunter(ids[0]);

        game.Arm(ids[0]);

        Assert.Null(game.EndsAt);
        Assert.Null(game.StartedAt);
        Assert.Equal(GameStatus.Started, game.Status);
    }

    [Fact]
    public void BeginPlay_ShouldSetEndsAt_ToStartedAtPlusGameDuration()
    {
        var config = GameFaker.ValidConfiguration(gameDuration: 60);
        var game = GameFaker.LobbyGameWithPlayers(2, out var ids, config);
        game.DesignateHunter(ids[0]);

        game.Arm(ids[0]);
        game.BeginPlay(Start);

        Assert.Equal(Start.AddMinutes(60), game.EndsAt);
    }

    [Fact]
    public void BeginPlay_ShouldSetEndsAt_MatchingConfiguredGameDuration()
    {
        var config = GameFaker.ValidConfiguration(gameDuration: 30);
        var game = GameFaker.LobbyGameWithPlayers(2, out var ids, config);
        var startTime = new DateTimeOffset(2026, 6, 9, 15, 0, 0, TimeSpan.Zero);
        game.DesignateHunter(ids[0]);

        game.Arm(ids[0]);
        game.BeginPlay(startTime);

        Assert.Equal(startTime.AddMinutes(30), game.EndsAt);
    }
}
