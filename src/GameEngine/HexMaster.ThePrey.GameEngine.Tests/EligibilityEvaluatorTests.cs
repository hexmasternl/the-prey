using HexMaster.ThePrey.GameEngine;
using HexMaster.ThePrey.Games.DomainModels;

namespace HexMaster.ThePrey.GameEngine.Tests;

public sealed class EligibilityEvaluatorTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 6, 5, 10, 0, 0, TimeSpan.Zero);

    // Game: 60-min duration, 5-min hunter delay, 10-min final stage, default 30s interval, final 10s interval
    private static Game CreateStartedGame(int playerCount = 3)
    {
        var config = GameConfiguration.Create(60, 5, 10, 30, 10);
        var game = Game.Create(Guid.NewGuid(), Guid.NewGuid(), "12345678", config);
        var ids = new List<Guid>();
        for (var i = 0; i < playerCount; i++)
        {
            var id = Guid.NewGuid();
            game.JoinLobby(LobbyPlayer.Create(id, $"Player{i}", null));
            ids.Add(id);
        }
        game.Start(ids[0], StartedAt);
        return game;
    }

    [Fact]
    public void GetEligible_ShouldIncludeParticipant_WhenNeverBroadcasted()
    {
        var game = CreateStartedGame();
        var now = StartedAt.AddSeconds(35);
        var lastBroadcasts = new Dictionary<Guid, DateTimeOffset>();

        var eligible = EligibilityEvaluator.GetEligible(game, now, lastBroadcasts);

        Assert.Equal(3, eligible.Count);
    }

    [Fact]
    public void GetEligible_ShouldExcludeParticipant_WhenNotYetDue()
    {
        var game = CreateStartedGame();
        var now = StartedAt.AddSeconds(35);

        var hunter = game.Hunter!;
        var lastBroadcasts = new Dictionary<Guid, DateTimeOffset>
        {
            [hunter.UserId] = now.AddSeconds(-10) // only 10s ago, interval is 30s
        };

        var eligible = EligibilityEvaluator.GetEligible(game, now, lastBroadcasts);

        Assert.DoesNotContain(eligible, p => p.UserId == hunter.UserId);
    }

    [Fact]
    public void GetEligible_ShouldIncludeParticipant_WhenIntervalElapsed()
    {
        var game = CreateStartedGame();
        var now = StartedAt.AddSeconds(60);

        var hunter = game.Hunter!;
        var lastBroadcasts = new Dictionary<Guid, DateTimeOffset>
        {
            [hunter.UserId] = now.AddSeconds(-30) // exactly 30s ago, interval is 30s
        };

        var eligible = EligibilityEvaluator.GetEligible(game, now, lastBroadcasts);

        Assert.Contains(eligible, p => p.UserId == hunter.UserId);
    }

    [Fact]
    public void GetEligible_ShouldUsePenaltyInterval_WhenParticipantHasActivePenalty()
    {
        var game = CreateStartedGame();
        var now = StartedAt.AddSeconds(60);

        // Apply a penalty to a prey (penalty reporting interval is 10s instead of 30s)
        var prey = game.Preys[0];
        game.ApplyPenalty(prey.UserId, now.AddSeconds(60));

        var lastBroadcasts = new Dictionary<Guid, DateTimeOffset>
        {
            [prey.UserId] = now.AddSeconds(-15) // 15s ago; would not be due at 30s but is due at 10s
        };

        var eligible = EligibilityEvaluator.GetEligible(game, now, lastBroadcasts);

        Assert.Contains(eligible, p => p.UserId == prey.UserId);
    }

    [Fact]
    public void GetEligible_ShouldUseFinalStageInterval_WhenInFinalStage()
    {
        // Final stage starts at 60 - 10 = 50 minutes in
        var game = CreateStartedGame();
        var now = StartedAt.AddMinutes(52); // 2 min into final stage, interval is 10s

        var prey = game.Preys[0];
        var lastBroadcasts = new Dictionary<Guid, DateTimeOffset>
        {
            [prey.UserId] = now.AddSeconds(-15) // 15s ago; due at 10s interval
        };

        var eligible = EligibilityEvaluator.GetEligible(game, now, lastBroadcasts);

        Assert.Contains(eligible, p => p.UserId == prey.UserId);
    }

    [Fact]
    public void GetEligible_ShouldNotIncludeFinalStageParticipant_WhenNotDue()
    {
        var game = CreateStartedGame();
        var now = StartedAt.AddMinutes(52);

        var prey = game.Preys[0];
        var lastBroadcasts = new Dictionary<Guid, DateTimeOffset>
        {
            [prey.UserId] = now.AddSeconds(-5) // only 5s ago, interval is 10s
        };

        var eligible = EligibilityEvaluator.GetEligible(game, now, lastBroadcasts);

        Assert.DoesNotContain(eligible, p => p.UserId == prey.UserId);
    }
}
