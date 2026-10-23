using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Tests.Factories;

namespace HexMaster.ThePrey.Games.Tests.DomainModels;

public sealed class GameSetHunterTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SetHunter_ShouldChangeHunterUserId_WhenNewHunterIsAPrey()
    {
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Start);
        var newHunterId = preyIds[0];

        game.SetHunter(newHunterId);

        Assert.Equal(newHunterId, game.HunterUserId);
        Assert.Contains(game.Preys, id => id == hunterId);
        Assert.DoesNotContain(game.Preys, id => id == newHunterId);
        Assert.Equal(GameStatus.InProgress, game.Status);
    }

    [Fact]
    public void SetHunter_ShouldKeepParticipantCount_WhenHunterChanges()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start, playerCount: 4);

        game.SetHunter(preyIds[1]);

        Assert.Equal(3, game.Preys.Count);
        Assert.NotNull(game.HunterUserId);
    }

    [Fact]
    public void SetHunter_ShouldThrow_WhenNewHunterIsCurrentHunter()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, Start);

        Assert.Throws<ArgumentException>(() => game.SetHunter(hunterId));
        Assert.Equal(hunterId, game.HunterUserId);
    }

    [Fact]
    public void SetHunter_ShouldThrow_WhenNewHunterIsNotAParticipant()
    {
        var game = GameFaker.StartedGame(out var hunterId, out _, Start);

        Assert.Throws<ArgumentException>(() => game.SetHunter(Guid.NewGuid()));
        Assert.Equal(hunterId, game.HunterUserId);
    }

    [Fact]
    public void SetHunter_ShouldThrow_WhenGameIsInLobby()
    {
        var game = GameFaker.LobbyGameWithPlayers(3, out var ids);

        Assert.Throws<InvalidOperationException>(() => game.SetHunter(ids[0]));
        Assert.Equal(GameStatus.Lobby, game.Status);
    }

    [Fact]
    public void SetHunter_ShouldThrow_WhenGameIsCompleted()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Start);
        game.Complete(Start.AddHours(1));

        Assert.Throws<InvalidOperationException>(() => game.SetHunter(preyIds[0]));
        Assert.Equal(GameStatus.Completed, game.Status);
    }
}
