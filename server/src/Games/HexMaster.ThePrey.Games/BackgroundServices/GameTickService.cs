using HexMaster.ThePrey.Games.DomainModels;
using HexMaster.ThePrey.Games.GameEngine;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Games.BackgroundServices;

/// <summary>
/// Drives the shared game sweep on a fixed cadence. Replaces both the old per-game GameEngine job and
/// the PlayerStateMonitor: a single leader-elected sweep runs every <see cref="Interval"/> across all
/// in-progress games. The actual work lives in <see cref="IGameTickRunner"/> so the trigger can be
/// swapped (e.g. for a Dapr cron binding) without touching the sweep logic.
/// </summary>
public sealed class GameTickService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(Game.SweepIntervalSeconds);

    private readonly IGameTickRunner _runner;
    private readonly ILogger<GameTickService> _logger;

    public GameTickService(IGameTickRunner runner, ILogger<GameTickService> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GameTickService started; sweeping every {Seconds}s.", Interval.TotalSeconds);

        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await _runner.RunTickAsync(stoppingToken);
        }
    }
}
