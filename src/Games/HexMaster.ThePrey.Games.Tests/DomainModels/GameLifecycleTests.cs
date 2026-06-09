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

    // ── Task 5.2: EndsAt set on Start ─────────────────────────────────────────

    [Fact]
    public void Create_ShouldLeaveEndsAt_Null()
    {
        var game = GameFaker.LobbyGame();

        Assert.Null(game.EndsAt);
    }

    [Fact]
    public void Start_ShouldSetEndsAt_ToStartedAtPlusGameDuration()
    {
        var config = GameFaker.ValidConfiguration(gameDuration: 60);
        var game = GameFaker.LobbyGameWithPlayers(2, out var ids, config);

        game.Start(ids[0], Start);

        Assert.Equal(Start.AddMinutes(60), game.EndsAt);
    }

    [Fact]
    public void Start_ShouldSetEndsAt_MatchingConfiguredGameDuration()
    {
        var config = GameFaker.ValidConfiguration(gameDuration: 30);
        var game = GameFaker.LobbyGameWithPlayers(2, out var ids, config);
        var startTime = new DateTimeOffset(2026, 6, 9, 15, 0, 0, TimeSpan.Zero);

        game.Start(ids[0], startTime);

        Assert.Equal(startTime.AddMinutes(30), game.EndsAt);
    }
}
