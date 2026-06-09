using HexMaster.ThePrey.Games.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace HexMaster.ThePrey.Games.BackgroundServices;

public sealed class GameCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GameCleanupService> _logger;

    public GameCleanupService(IServiceScopeFactory scopeFactory, ILogger<GameCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCleanupAsync(stoppingToken);
        }
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        using var activity = GameActivitySource.Source.StartActivity("GameCleanup");
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
            var cutoff = DateTimeOffset.UtcNow;
            var deleted = await repo.DeleteExpiredGamesAsync(cutoff, ct);
            activity?.SetTag("game.cleanup.deleted_count", deleted);
            _logger.LogInformation("Game cleanup removed {Count} expired games.", deleted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            _logger.LogError(ex, "Game cleanup failed.");
        }
    }
}
