using HexMaster.ThePrey.Games.GameEngine;
using HexMaster.ThePrey.Games.LeaderElection;
using HexMaster.ThePrey.Games.Observability;
using HexMaster.ThePrey.Games.Tests.Factories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.GameEngine;

public sealed class GameTickRunnerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IGameRepository> _games = new();
    private readonly Mock<IGameSweepProcessor> _processor = new();
    private readonly Mock<ILeaderElection> _leader = new();
    private readonly Mock<IGameMetrics> _metrics = new();
    private readonly GameTickRunner _sut;

    public GameTickRunnerTests()
    {
        _processor.Setup(p => p.ProcessAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GameTickResult.None);

        var services = new ServiceCollection();
        services.AddScoped(_ => _games.Object);
        services.AddScoped(_ => _processor.Object);
        var provider = services.BuildServiceProvider();

        _sut = new GameTickRunner(
            provider.GetRequiredService<IServiceScopeFactory>(),
            _leader.Object,
            _metrics.Object,
            new FixedTimeProvider(Now),
            NullLogger<GameTickRunner>.Instance);
    }

    [Fact]
    public async Task RunTickAsync_ShouldNotSweep_WhenNotLeader()
    {
        _leader.Setup(l => l.TryAcquireAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await _sut.RunTickAsync(CancellationToken.None);

        _games.Verify(r => r.GetInProgressGameIdsAsync(It.IsAny<CancellationToken>()), Times.Never);
        _processor.Verify(p => p.ProcessAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunTickAsync_ShouldProcessEveryInProgressGame_WhenLeader()
    {
        _leader.Setup(l => l.TryAcquireAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        _games.Setup(r => r.GetInProgressGameIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(ids);

        await _sut.RunTickAsync(CancellationToken.None);

        foreach (var id in ids)
            _processor.Verify(p => p.ProcessAsync(id, Now, It.IsAny<CancellationToken>()), Times.Once);
        _metrics.Verify(m => m.RecordSweepTick(ids.Length, It.IsAny<double>()), Times.Once);
    }

    [Fact]
    public async Task RunTickAsync_ShouldRecordLeadershipChange_OnlyOnTransition()
    {
        _leader.Setup(l => l.TryAcquireAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _games.Setup(r => r.GetInProgressGameIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await _sut.RunTickAsync(CancellationToken.None);
        await _sut.RunTickAsync(CancellationToken.None); // still leader → no second change

        _metrics.Verify(m => m.RecordLeadershipChanged(true), Times.Once);
    }
}
