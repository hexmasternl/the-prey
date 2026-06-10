using System.Diagnostics;
using HexMaster.ThePrey.Games.LeaderElection;
using HexMaster.ThePrey.Games.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Games.GameEngine;

/// <summary>
/// Default <see cref="IGameTickRunner"/>. Singleton: it holds the non-reentrancy gate and the
/// leadership state across ticks. Each game is processed in its own DI scope (its own DbContext) with
/// bounded parallelism so thousands of games complete within the tick budget without contending on a
/// single context.
/// </summary>
public sealed class GameTickRunner : IGameTickRunner
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILeaderElection _leaderElection;
    private readonly IGameMetrics _metrics;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GameTickRunner> _logger;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly int _maxParallelism = Math.Max(4, Environment.ProcessorCount * 2);
    private bool _wasLeader;

    public GameTickRunner(
        IServiceScopeFactory scopeFactory,
        ILeaderElection leaderElection,
        IGameMetrics metrics,
        TimeProvider timeProvider,
        ILogger<GameTickRunner> logger)
    {
        _scopeFactory = scopeFactory;
        _leaderElection = leaderElection;
        _metrics = metrics;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task RunTickAsync(CancellationToken ct)
    {
        // Non-reentrant: if the previous tick is still running, skip this one and record the overrun.
        if (!await _gate.WaitAsync(0, ct))
        {
            _metrics.RecordSweepOverrun();
            _logger.LogWarning("Previous sweep tick is still running; skipping this tick.");
            return;
        }

        try
        {
            using var activity = GameActivitySource.Source.StartActivity("GameTick");

            var isLeader = await _leaderElection.TryAcquireAsync(ct);
            if (isLeader != _wasLeader)
            {
                _metrics.RecordLeadershipChanged(isLeader);
                _wasLeader = isLeader;
                _logger.LogInformation("Sweep leadership {State}.", isLeader ? "acquired" : "lost");
            }

            activity?.SetTag("sweep.leader", isLeader);
            if (!isLeader)
                return;

            await SweepAsync(activity, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Sweep tick failed.");
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task SweepAsync(Activity? activity, CancellationToken ct)
    {
        var start = Stopwatch.GetTimestamp();
        var now = _timeProvider.GetUtcNow();

        IReadOnlyList<Guid> ids;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
            ids = await repo.GetInProgressGameIdsAsync(ct);
        }

        var totals = new SweepTotals();
        var sync = new object();

        await Parallel.ForEachAsync(
            ids,
            new ParallelOptions { MaxDegreeOfParallelism = _maxParallelism, CancellationToken = ct },
            async (gameId, token) =>
            {
                try
                {
                    await using var gameScope = _scopeFactory.CreateAsyncScope();
                    var processor = gameScope.ServiceProvider.GetRequiredService<IGameSweepProcessor>();
                    var result = await processor.ProcessAsync(gameId, now, token);

                    lock (sync)
                    {
                        totals.Transitions += result.Transitions;
                        totals.Broadcasts += result.Broadcasts;
                        totals.Penalties += result.Penalties;
                        if (result.Completed) totals.Completions++;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Sweep failed for game {GameId}.", gameId);
                }
            });

        var elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        _metrics.RecordSweepTick(ids.Count, elapsedMs);

        activity?.SetTag("sweep.games", ids.Count);
        activity?.SetTag("sweep.transitions", totals.Transitions);
        activity?.SetTag("sweep.broadcasts", totals.Broadcasts);
        activity?.SetTag("sweep.penalties", totals.Penalties);
        activity?.SetTag("sweep.completions", totals.Completions);

        _logger.LogInformation(
            "Sweep processed {Games} games in {ElapsedMs:F0}ms ({Transitions} transitions, {Broadcasts} broadcasts, {Penalties} penalties, {Completions} completions).",
            ids.Count, elapsedMs, totals.Transitions, totals.Broadcasts, totals.Penalties, totals.Completions);
    }

    private sealed class SweepTotals
    {
        public int Transitions;
        public int Broadcasts;
        public int Penalties;
        public int Completions;
    }
}
