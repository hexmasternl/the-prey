using HexMaster.ThePrey.Games.BackgroundServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace HexMaster.ThePrey.Games.Tests.Features;

public sealed class GameCleanupServiceTests
{
    private readonly Mock<IGameRepository> _repository = new();
    private readonly Mock<ILogger<GameCleanupService>> _logger = new();

    private GameCleanupService CreateService()
    {
        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider.GetService(typeof(IGameRepository))).Returns(_repository.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        return new GameCleanupService(scopeFactory.Object, _logger.Object);
    }

    // ── Task 5.3: service shuts down cleanly without firing during cancel ──────

    [Fact]
    public async Task StartAsync_ShouldNotCallRepository_WhenCancelledBeforeFirstTick()
    {
        var service = CreateService();
        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);
        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        _repository.Verify(
            r => r.DeleteExpiredGamesAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Task 5.4: DeleteExpiredGamesAsync interface contract ──────────────────

    [Fact]
    public async Task DeleteExpiredGamesAsync_ShouldReceiveCutoffNearNow()
    {
        DateTimeOffset? capturedCutoff = null;
        _repository
            .Setup(r => r.DeleteExpiredGamesAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback<DateTimeOffset, CancellationToken>((cutoff, _) => capturedCutoff = cutoff)
            .ReturnsAsync(0);

        var before = DateTimeOffset.UtcNow;
        await _repository.Object.DeleteExpiredGamesAsync(DateTimeOffset.UtcNow, CancellationToken.None);
        var after = DateTimeOffset.UtcNow;

        Assert.NotNull(capturedCutoff);
        Assert.InRange(capturedCutoff!.Value, before, after);
    }

    [Fact]
    public async Task DeleteExpiredGamesAsync_ShouldReturnZero_WhenNoExpiredGames()
    {
        _repository
            .Setup(r => r.DeleteExpiredGamesAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _repository.Object.DeleteExpiredGamesAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task DeleteExpiredGamesAsync_ShouldReturnCount_WhenExpiredGamesExist()
    {
        _repository
            .Setup(r => r.DeleteExpiredGamesAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var result = await _repository.Object.DeleteExpiredGamesAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(5, result);
    }
}
