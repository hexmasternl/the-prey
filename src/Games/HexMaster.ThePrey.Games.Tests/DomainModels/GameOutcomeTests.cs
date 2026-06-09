using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Tests.Factories;

namespace HexMaster.ThePrey.Games.Tests.DomainModels;

public sealed class GameOutcomeTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);

    // ── Complete (natural end-of-game) ─────────────────────────────────────

    [Fact]
    public void Complete_ShouldSetStatusToCompleted_WhenGameIsInProgress()
    {
        var game = GameFaker.StartedGame(out _, out _, Now);

        game.Complete(Now.AddMinutes(60));

        Assert.Equal(GameStatus.Completed, game.Status);
    }

    [Fact]
    public void Complete_ShouldSetCompletedAt_WhenGameIsInProgress()
    {
        var at = Now.AddMinutes(60);
        var game = GameFaker.StartedGame(out _, out _, Now);

        game.Complete(at);

        Assert.Equal(at, game.CompletedAt);
    }

    [Fact]
    public void Complete_ShouldReturnPreysWin_WhenAtLeastOnePreySurvives()
    {
        // 1 hunter + 2 preys; no prey is tagged
        var game = GameFaker.StartedGame(out _, out _, Now, playerCount: 3);

        game.Complete(Now.AddMinutes(60));

        Assert.Equal(GameOutcome.PreysWin, game.Outcome);
    }

    [Fact]
    public void Complete_ShouldReturnHuntersWin_WhenAllPreysAreTagged()
    {
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Now, playerCount: 3);
        foreach (var preyId in preyIds)
            game.TagParticipant(hunterId, preyId);

        game.Complete(Now.AddMinutes(60));

        Assert.Equal(GameOutcome.HuntersWin, game.Outcome);
    }

    [Fact]
    public void Complete_ShouldReturnHuntersWin_WhenAllPreysAreOut()
    {
        var game = GameFaker.StartedGame(out _, out var preyIds, Now, playerCount: 3);
        foreach (var preyId in preyIds)
            game.Forfeit(preyId);

        game.Complete(Now.AddMinutes(60));

        Assert.Equal(GameOutcome.HuntersWin, game.Outcome);
    }

    [Fact]
    public void Complete_ShouldReturnHuntersWin_WhenSomePreysTaggedAndRestOut()
    {
        // 1 hunter + 3 preys
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Now, playerCount: 4);
        game.TagParticipant(hunterId, preyIds[0]);
        game.Forfeit(preyIds[1]);
        game.TagParticipant(hunterId, preyIds[2]);

        game.Complete(Now.AddMinutes(60));

        Assert.Equal(GameOutcome.HuntersWin, game.Outcome);
    }

    [Fact]
    public void Complete_ShouldReturnPreysWin_WhenOnlySomePreysAreTagged()
    {
        // 1 hunter + 2 preys; only 1 tagged
        var game = GameFaker.StartedGame(out var hunterId, out var preyIds, Now, playerCount: 3);
        game.TagParticipant(hunterId, preyIds[0]);

        game.Complete(Now.AddMinutes(60));

        Assert.Equal(GameOutcome.PreysWin, game.Outcome);
    }

    [Fact]
    public void Complete_ShouldThrow_WhenGameIsAlreadyCompleted()
    {
        var game = GameFaker.StartedGame(out _, out _, Now);
        game.Complete(Now.AddMinutes(60));

        var ex = Assert.Throws<InvalidOperationException>(() => game.Complete(Now.AddMinutes(70)));
        Assert.Contains("in-progress", ex.Message);
    }

    [Fact]
    public void Complete_ShouldThrow_WhenGameIsInLobby()
    {
        var game = GameFaker.LobbyGame();

        Assert.Throws<InvalidOperationException>(() => game.Complete(Now));
    }

    // ── EndByOwner ─────────────────────────────────────────────────────────

    [Fact]
    public void EndByOwner_ShouldSetOutcomeCancelled_WhenGameIsInLobby()
    {
        var game = GameFaker.LobbyGameWithPlayers(2, out _);

        game.EndByOwner(Now);

        Assert.Equal(GameOutcome.Cancelled, game.Outcome);
        Assert.Equal(GameStatus.Completed, game.Status);
        Assert.Equal(Now, game.CompletedAt);
    }

    [Fact]
    public void EndByOwner_ShouldComputeOutcome_WhenGameIsInProgress()
    {
        var game = GameFaker.StartedGame(out _, out _, Now, playerCount: 3);

        game.EndByOwner(Now.AddMinutes(30));

        Assert.Equal(GameStatus.Completed, game.Status);
        // Preys are still alive (Active) → PreysWin
        Assert.Equal(GameOutcome.PreysWin, game.Outcome);
    }

    [Fact]
    public void EndByOwner_ShouldThrow_WhenGameIsAlreadyCompleted()
    {
        var game = GameFaker.StartedGame(out _, out _, Now);
        game.EndByOwner(Now.AddMinutes(30));

        Assert.Throws<InvalidOperationException>(() => game.EndByOwner(Now.AddMinutes(31)));
    }

    // ── Rehydrate ──────────────────────────────────────────────────────────

    [Fact]
    public void Rehydrate_ShouldRestoreOutcomeAndCompletedAt()
    {
        var completedAt = Now.AddMinutes(60);
        var original = GameFaker.StartedGame(out _, out _, Now);
        original.Complete(completedAt);

        var rehydrated = Game.Rehydrate(
            original.Id,
            original.GameCode,
            original.OwnerUserId,
            original.PlayfieldId,
            original.Status,
            original.Configuration,
            original.StartedAt,
            original.Lobby,
            [],
            original.DesignatedHunterUserId,
            original.CreatedAt,
            original.EndsAt,
            original.CleanUpAfter,
            completedAt: completedAt,
            outcome: GameOutcome.PreysWin);

        Assert.Equal(GameOutcome.PreysWin, rehydrated.Outcome);
        Assert.Equal(completedAt, rehydrated.CompletedAt);
    }

    [Fact]
    public void Rehydrate_ShouldDefaultToUndecided_WhenOutcomeNotProvided()
    {
        var game = GameFaker.LobbyGame();
        var rehydrated = Game.Rehydrate(
            game.Id,
            game.GameCode,
            game.OwnerUserId,
            game.PlayfieldId,
            game.Status,
            game.Configuration,
            game.StartedAt,
            game.Lobby,
            []);

        Assert.Equal(GameOutcome.Undecided, rehydrated.Outcome);
        Assert.Null(rehydrated.CompletedAt);
    }
}
