using System.Diagnostics;
using HexMaster.ThePrey.Games.Notifications;
using HexMaster.ThePrey.Games.Observability;
using HexMaster.ThePrey.IntegrationEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HexMaster.ThePrey.Games.BackgroundServices;

public sealed class PlayerStateMonitor : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PlayerStateMonitor> _logger;

    public PlayerStateMonitor(IServiceScopeFactory scopeFactory, ILogger<PlayerStateMonitor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunAsync(stoppingToken);
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var activity = GameActivitySource.Source.StartActivity("PlayerStateMonitor");
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
            var eventBus = scope.ServiceProvider.GetRequiredService<IGameEventBus>();

            var now = DateTimeOffset.UtcNow;
            var games = await repo.GetAllInProgressAsync(ct);
            var totalTransitions = 0;

            foreach (var game in games)
            {
                var changes = game.ApplyTimeoutTransitions(now);
                if (changes.Count == 0) continue;

                await repo.UpdateAsync(game, ct);
                totalTransitions += changes.Count;

                foreach (var (userId, _) in changes)
                {
                    await eventBus.PublishAsync(game.Id, RealtimeProtocol.MessageTypes.ParticipantChanged,
                        game.ToParticipantDto(userId), ct);
                }
            }

            activity?.SetTag("game.state_monitor.transitions", totalTransitions);
            _logger.LogInformation("PlayerStateMonitor applied {Count} state transitions.", totalTransitions);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            _logger.LogError(ex, "PlayerStateMonitor encountered an error.");
        }
    }
}
