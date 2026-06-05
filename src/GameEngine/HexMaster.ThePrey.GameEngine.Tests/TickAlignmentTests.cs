using HexMaster.ThePrey.GameEngine;

namespace HexMaster.ThePrey.GameEngine.Tests;

public sealed class TickAlignmentTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 5, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ComputeNextTick_ShouldReturnFirstTick_WhenEngineStartsBeforeFirstTick()
    {
        var now = T0.AddSeconds(15);

        var nextTick = GameLocationChecker.ComputeNextTick(T0, now, 30);

        Assert.Equal(T0.AddSeconds(30), nextTick);
    }

    [Fact]
    public void ComputeNextTick_ShouldSkipElapsedTicks_WhenEngineStartsLate()
    {
        var now = T0.AddSeconds(95);

        var nextTick = GameLocationChecker.ComputeNextTick(T0, now, 30);

        Assert.Equal(T0.AddSeconds(120), nextTick);
    }

    [Fact]
    public void ComputeNextTick_ShouldReturnNextTick_WhenNowIsExactlyOnATick()
    {
        // When now == T0+60, the next tick should be T0+90 (one full interval forward)
        var now = T0.AddSeconds(60);

        var nextTick = GameLocationChecker.ComputeNextTick(T0, now, 30);

        Assert.Equal(T0.AddSeconds(90), nextTick);
    }

    [Fact]
    public void ComputeNextTick_ShouldReturnStartPlusInterval_WhenNowEqualsStartTime()
    {
        var nextTick = GameLocationChecker.ComputeNextTick(T0, T0, 30);

        Assert.Equal(T0.AddSeconds(30), nextTick);
    }

    [Fact]
    public void ComputeNextTick_ShouldAlignToInterval_WithFinalStageInterval()
    {
        // Final stage uses 10-second intervals
        var now = T0.AddSeconds(7);

        var nextTick = GameLocationChecker.ComputeNextTick(T0, now, 10);

        Assert.Equal(T0.AddSeconds(10), nextTick);
    }
}
