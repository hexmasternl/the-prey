using HexMaster.ThePrey.Games.DomainModels;

namespace HexMaster.ThePrey.Games.Tests.DomainModels;

public sealed class GameConfigurationTests
{
    [Fact]
    public void Create_ShouldSucceed_WhenAllValuesAreValid()
    {
        var configuration = GameConfiguration.Create(60, 5, 10, 30, 10, true, true);

        Assert.Equal(60, configuration.GameDuration);
        Assert.Equal(5, configuration.HunterDelayTime);
        Assert.Equal(10, configuration.FinalStageDuration);
        Assert.Equal(30, configuration.DefaultLocationInterval);
        Assert.Equal(10, configuration.FinalLocationInterval);
        Assert.True(configuration.EnablePreyBoundaryPenalties);
        Assert.True(configuration.EnableHunterBoundaryPenalty);
    }

    [Fact]
    public void Create_ShouldDefaultBoundaryPenaltiesToDisabled_WhenNotSpecified()
    {
        var configuration = GameConfiguration.Create(60, 5, 10, 30, 10);

        Assert.False(configuration.EnablePreyBoundaryPenalties);
        Assert.False(configuration.EnableHunterBoundaryPenalty);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_ShouldThrow_WhenGameDurationIsNotPositive(int gameDuration)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GameConfiguration.Create(gameDuration, 0, 1, 30, 10));
    }

    [Theory]
    [InlineData(60, 60)] // equal to duration
    [InlineData(60, 90)] // greater than duration
    public void Create_ShouldThrow_WhenFinalStageIsNotShorterThanGame(int gameDuration, int finalStageDuration)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GameConfiguration.Create(gameDuration, 5, finalStageDuration, 30, 10));
    }

    [Fact]
    public void Create_ShouldThrow_WhenFinalStageIsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GameConfiguration.Create(60, 5, 0, 30, 10));
    }

    [Theory]
    [InlineData(60, 60)] // equal to duration
    [InlineData(60, 90)] // greater than duration
    [InlineData(60, -1)] // negative
    public void Create_ShouldThrow_WhenHunterDelayIsInvalid(int gameDuration, int hunterDelayTime)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GameConfiguration.Create(gameDuration, hunterDelayTime, 10, 30, 10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_ShouldThrow_WhenDefaultIntervalIsNotPositive(int defaultLocationInterval)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GameConfiguration.Create(60, 5, 10, defaultLocationInterval, 10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_ShouldThrow_WhenFinalIntervalIsNotPositive(int finalLocationInterval)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GameConfiguration.Create(60, 5, 10, 30, finalLocationInterval));
    }

    [Fact]
    public void Create_ShouldThrow_WhenFinalIntervalSlowerThanDefault()
    {
        Assert.Throws<ArgumentException>(() => GameConfiguration.Create(60, 5, 10, 30, 31));
    }
}
