using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.Tests.Factories;

namespace HexMaster.ThePrey.Games.Tests.DomainModels;

public sealed class GameTimingTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 3, 12, 0, 0, TimeSpan.Zero);

    // GameDuration 60, HunterDelay 5, FinalStage 10, Default 30s, Final 10s.
    // Scheduled end: Start + 60m. Final stage: [Start + 50m, Start + 60m).
    private static Game StartedGame(out Guid hunterId, out Guid preyId)
    {
        var game = GameFaker.StartedGame(out hunterId, out var preyIds, Start, playerCount: 3, configuration: GameFaker.ValidConfiguration());
        preyId = preyIds[0];
        return game;
    }

    [Fact]
    public void ReportingIntervalFor_ShouldBeDefault_BeforeFinalStage()
    {
        var game = StartedGame(out _, out var preyId);

        Assert.Equal(30, game.ReportingIntervalFor(preyId, Start.AddMinutes(10)));
    }

    [Fact]
    public void ReportingIntervalFor_ShouldBeFinalInterval_DuringFinalStage()
    {
        var game = StartedGame(out _, out var preyId);

        Assert.Equal(10, game.ReportingIntervalFor(preyId, Start.AddMinutes(55)));
    }

    [Fact]
    public void ReportingIntervalFor_ShouldBePenaltyInterval_WhenPenaltyActive_RegardlessOfStage()
    {
        var game = StartedGame(out _, out var preyId);
        game.ApplyPenalty(preyId, Start.AddMinutes(15));

        // Before the final stage, but penalised — 10 second penalty cadence takes precedence.
        Assert.Equal(Game.PenaltyReportingIntervalSeconds, game.ReportingIntervalFor(preyId, Start.AddMinutes(10)));
    }

    [Fact]
    public void ReportingIntervalFor_ShouldReturnToStageInterval_WhenPenaltyExpired()
    {
        var game = StartedGame(out _, out var preyId);
        game.ApplyPenalty(preyId, Start.AddMinutes(10));

        // Penalty has expired by minute 20 — back to the default interval.
        Assert.Equal(30, game.ReportingIntervalFor(preyId, Start.AddMinutes(20)));
    }

    [Theory]
    [InlineData(49, false)]
    [InlineData(50, true)]
    [InlineData(55, true)]
    [InlineData(60, false)] // at the scheduled end the game is over, not in the final stage
    public void IsInFinalStage_ShouldRespectBoundaries(int minutesAfterStart, bool expected)
    {
        var game = StartedGame(out _, out _);

        Assert.Equal(expected, game.IsInFinalStage(Start.AddMinutes(minutesAfterStart)));
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    public void AreHuntersAllowedToMove_ShouldRespectHeadStart(int minutesAfterStart, bool expected)
    {
        var game = StartedGame(out _, out _);

        Assert.Equal(expected, game.AreHuntersAllowedToMove(Start.AddMinutes(minutesAfterStart)));
    }

    [Fact]
    public void HasActivePenalty_ShouldBeFalse_WhenAllPenaltiesExpired()
    {
        var game = StartedGame(out _, out var preyId);
        game.ApplyPenalty(preyId, Start.AddMinutes(10));

        var prey = game.Participants.Single(p => p.UserId == preyId);
        Assert.False(prey.HasActivePenalty(Start.AddMinutes(20)));
        Assert.True(prey.HasActivePenalty(Start.AddMinutes(5)));
    }
}
